using IdentityServer.Application.DTOs.AuthorizationCode;
using IdentityServer.Application.Interfaces;
using IdentityServer.Domain.Entities;
using IdentityServer.Domain.Interfaces;
using IdentityServer.Shared.Common;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace IdentityServer.Application.Services;

/// <summary>
/// Service for processing OAuth 2.0 authorization endpoint requests
/// Implements RFC 6749 Section 3.1 (Authorization Endpoint) and RFC 7636 (PKCE)
/// </summary>
/// <remarks>
/// Errors are reported in two ways, as RFC 6749 §4.1.2.1 requires:
/// <list type="bullet">
/// <item>If the client or redirect_uri is invalid, the result is a <b>failure</b>. The caller must show an
/// error page and must NOT redirect, otherwise the server would become an open redirector.</item>
/// <item>Any later error is a <b>success</b> with Action = "error" and a redirect URI that carries
/// <c>error</c>, <c>error_description</c> and <c>state</c> back to the client.</item>
/// </list>
/// </remarks>
public class AuthorizationEndpointService(
    IRepository<Client> clientRepository,
    IRepository<Scope> scopeRepository,
    IUserConsentService userConsentService,
    IClientScopeRepository clientScopeRepository,
    IAuthorizationService authorizationService,
    ILogger<AuthorizationEndpointService> logger)
    : IAuthorizationEndpointService
{
    private static readonly string[] SupportedChallengeMethods = ["S256", "plain"];

    public async Task<Result<AuthorizationCodeResponse>> ProcessAuthorizeRequestAsync(
        AuthorizationCodeRequest request,
        long userId,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Validate client and redirect URI (failures here must never redirect)
        var clientResult = await ValidateClientAndRedirectUriAsync(request, cancellationToken);
        if (!clientResult.IsSuccess)
        {
            return Result<AuthorizationCodeResponse>.Failure(clientResult.Errors!);
        }

        var client = clientResult.Data!;

        // Step 2: Validate response_type
        if (string.IsNullOrWhiteSpace(request.ResponseType))
            return ErrorRedirect(request, "invalid_request", "response_type is required");

        if (request.ResponseType != "code")
            return ErrorRedirect(request, "unsupported_response_type", "Only 'code' is supported");

        // Step 3: Validate PKCE (required for public clients)
        var pkceError = ValidatePkce(request, client);
        if (pkceError is not null)
            return ErrorRedirect(request, "invalid_request", pkceError);

        // Step 4: Validate requested scopes
        var scopeError = await ValidateScopesAsync(request.Scope, client.Id, cancellationToken);
        if (scopeError is not null)
            return ErrorRedirect(request, "invalid_scope", scopeError);

        var requestedScopes = ParseScopes(request.Scope);

        // Step 5: Check if user has already granted consent
        var hasConsent = await userConsentService.HasConsentAsync(
            userId,
            client.Id,
            request.Scope,
            cancellationToken);

        if (!hasConsent)
        {
            logger.LogInformation(
                "User {UserId} needs to grant consent for client {ClientId} with scopes: {Scopes}",
                userId,
                client.ClientIdentifier,
                request.Scope);

            return Result<AuthorizationCodeResponse>.Success(new AuthorizationCodeResponse
            {
                Action = "consent",
                ClientName = client.ClientName,
                Scopes = requestedScopes,
                State = request.State
            });
        }

        // Step 6: Generate authorization code
        var codeResult = await authorizationService.GenerateAuthorizationCodeAsync(
            userId,
            client.Id,
            requestedScopes.ToArray(),
            request.RedirectUri,
            request.CodeChallenge,
            request.CodeChallengeMethod,
            request.Nonce,
            cancellationToken);

        if (!codeResult.IsSuccess)
        {
            logger.LogError(
                "Failed to generate authorization code for user {UserId}, client {ClientId}",
                userId,
                client.ClientIdentifier);
            return ErrorRedirect(request, "server_error", "Failed to generate authorization code");
        }

        var authCode = codeResult.Data!;

        // Step 7: Build redirect URI with authorization code and state
        var redirectUri = BuildRedirectUri(request.RedirectUri, new Dictionary<string, string?>
        {
            ["code"] = authCode.Code,
            ["state"] = request.State
        });

        logger.LogInformation(
            "Authorization code {CodeId} generated successfully for user {UserId}, client {ClientId}",
            authCode.Id,
            userId,
            client.ClientIdentifier);

        return Result<AuthorizationCodeResponse>.Success(new AuthorizationCodeResponse
        {
            Action = "redirect",
            RedirectUri = redirectUri,
            ClientName = client.ClientName,
            Scopes = requestedScopes,
            State = request.State
        });
    }

    public async Task<Result<Client>> ValidateClientAndRedirectUriAsync(
        AuthorizationCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return Result<Client>.Failure("invalid_request: client_id is required");
        }

        if (string.IsNullOrWhiteSpace(request.RedirectUri))
        {
            return Result<Client>.Failure("invalid_request: redirect_uri is required");
        }

        var client = (await clientRepository.FindAsync(
                c => c.ClientIdentifier == request.ClientId && !c.IsDeleted,
                cancellationToken))
            .FirstOrDefault();

        if (client == null)
        {
            logger.LogWarning("Authorization request with invalid client_id: {ClientId}", request.ClientId);
            return Result<Client>.Failure("unauthorized_client: Unknown client");
        }

        if (!client.Enabled)
        {
            logger.LogWarning("Authorization request for disabled client: {ClientId}", request.ClientId);
            return Result<Client>.Failure("unauthorized_client: Client is disabled");
        }

        // Exact-match redirect URI validation (OAuth 2.0 Security BCP)
        var allowedUris = SplitList(client.RedirectUris);
        if (!allowedUris.Contains(request.RedirectUri, StringComparer.Ordinal))
        {
            logger.LogWarning(
                "Invalid redirect_uri {RedirectUri} for client {ClientId}",
                request.RedirectUri,
                request.ClientId);
            return Result<Client>.Failure("invalid_request: redirect_uri is not registered for this client");
        }

        if (!SplitList(client.AllowedGrantTypes).Contains("authorization_code", StringComparer.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Client {ClientId} does not support authorization_code grant type",
                request.ClientId);
            return Result<Client>.Failure("unauthorized_client: authorization_code grant not allowed");
        }

        return Result<Client>.Success(client);
    }

    public string BuildErrorRedirectUri(string redirectUri, string error, string description, string? state)
        => BuildRedirectUri(redirectUri, new Dictionary<string, string?>
        {
            ["error"] = error,
            ["error_description"] = description,
            ["state"] = state
        });

    private Result<AuthorizationCodeResponse> ErrorRedirect(
        AuthorizationCodeRequest request, string error, string description)
    {
        logger.LogWarning(
            "Authorization request for client {ClientId} rejected: {Error} - {Description}",
            request.ClientId, error, description);

        return Result<AuthorizationCodeResponse>.Success(new AuthorizationCodeResponse
        {
            Action = "error",
            RedirectUri = BuildErrorRedirectUri(request.RedirectUri, error, description, request.State),
            State = request.State
        });
    }

    private static string? ValidatePkce(AuthorizationCodeRequest request, Client client)
    {
        if (string.IsNullOrEmpty(request.CodeChallenge))
        {
            // Public clients cannot keep a secret, so PKCE is their proof of possession (RFC 7636, OAuth 2.0 BCP)
            return client.RequireClientSecret ? null : "code_challenge is required for public clients (PKCE)";
        }

        var method = request.CodeChallengeMethod ?? "plain";
        if (!SupportedChallengeMethods.Contains(method, StringComparer.Ordinal))
            return "code_challenge_method must be 'S256' or 'plain'";

        if (request.CodeChallenge.Length is < 43 or > 128)
            return "code_challenge must be between 43 and 128 characters";

        return null;
    }

    /// <summary>
    /// Validates that requested scopes exist and are allowed for the client. Returns an error message or null.
    /// </summary>
    private async Task<string?> ValidateScopesAsync(
        string scopeString,
        long clientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scopeString))
        {
            return "scope parameter is required";
        }

        var requestedScopes = ParseScopes(scopeString);

        var allScopes = await scopeRepository.GetAllAsync(cancellationToken);
        var validScopeNames = allScopes.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalidScopes = requestedScopes.Where(scope => !validScopeNames.Contains(scope)).ToList();
        if (invalidScopes.Count != 0)
        {
            return $"Unknown scopes: {string.Join(", ", invalidScopes)}";
        }

        var allowedScopeNames = (await clientScopeRepository.GetScopesAsync(clientId, cancellationToken))
            .Select(s => s.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var notAllowedScopes = requestedScopes.Where(scope => !allowedScopeNames.Contains(scope)).ToList();
        if (notAllowedScopes.Count != 0)
        {
            return $"Client not allowed to request: {string.Join(", ", notAllowedScopes)}";
        }

        return null;
    }

    private static List<string> ParseScopes(string scopeString) =>
        scopeString
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

    private static List<string> SplitList(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string BuildRedirectUri(string redirectUri, IDictionary<string, string?> parameters)
    {
        var nonEmpty = parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .ToDictionary(p => p.Key, p => p.Value);

        return QueryHelpers.AddQueryString(redirectUri, nonEmpty);
    }
}
