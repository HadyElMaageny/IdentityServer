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
/// Implements RFC 6749 Section 3.1 (Authorization Endpoint)
/// </summary>
public class AuthorizationEndpointService(
    IRepository<Client> clientRepository,
    IRepository<Scope> scopeRepository,
    IUserConsentService userConsentService,
    IClientScopeRepository clientScopeRepository,
    IAuthorizationService authorizationService,
    ILogger<AuthorizationEndpointService> logger)
    : IAuthorizationEndpointService
{
    public async Task<Result<AuthorizationCodeResponse>> ProcessAuthorizeRequestAsync(
        AuthorizationCodeRequest request,
        long userId,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Validate response_type
        var responseTypeValidation = ValidateResponseType(request.ResponseType);
        if (!responseTypeValidation.IsSuccess)
        {
            return Result<AuthorizationCodeResponse>.Failure(responseTypeValidation.Errors!);
        }

        // Step 2: Validate client and redirect URI
        var clientResult = await ValidateClientAndRedirectUriAsync(request, cancellationToken);
        if (!clientResult.IsSuccess)
        {
            return Result<AuthorizationCodeResponse>.Failure(clientResult.Errors!);
        }

        var client = clientResult.Data!;

        // Step 3: Validate requested scopes
        var scopeValidation = await ValidateScopesAsync(request.Scope, client.Id, cancellationToken);
        if (!scopeValidation.IsSuccess)
        {
            return Result<AuthorizationCodeResponse>.Failure(scopeValidation.Errors!);
        }

        var requestedScopes = ParseScopes(request.Scope);

        // Step 4: Check if user has already granted consent
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

        // Step 5: Generate authorization code
        var codeResult = await authorizationService.GenerateAuthorizationCodeAsync(
            userId,
            client.Id,
            requestedScopes.ToArray(),
            request.RedirectUri,
            cancellationToken);

        if (!codeResult.IsSuccess)
        {
            logger.LogError(
                "Failed to generate authorization code for user {UserId}, client {ClientId}",
                userId,
                client.ClientIdentifier);
            return Result<AuthorizationCodeResponse>.Failure(codeResult.Errors!);
        }

        var authCode = codeResult.Data!;

        // Step 6: Build redirect URI with authorization code and state
        var redirectUri = BuildSuccessRedirectUri(request.RedirectUri, authCode.Code, request.State);

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

    /// <summary>
    /// Validates the response_type parameter (RFC 6749 Section 3.1.1)
    /// </summary>
    private static Result<bool> ValidateResponseType(string responseType)
    {
        if (string.IsNullOrWhiteSpace(responseType))
        {
            return Result<bool>.Failure("invalid_request: response_type is required");
        }

        if (responseType != "code")
        {
            return Result<bool>.Failure("unsupported_response_type: Only 'code' is supported");
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Validates client credentials and redirect URI
    /// </summary>
    private async Task<Result<Client>> ValidateClientAndRedirectUriAsync(
        AuthorizationCodeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return Result<Client>.Failure("invalid_request: client_id is required");
        }

        if (string.IsNullOrWhiteSpace(request.RedirectUri))
        {
            return Result<Client>.Failure("invalid_request: redirect_uri is required");
        }

        // Find client
        var clients = await clientRepository.FindAsync(
            c => c.ClientIdentifier == request.ClientId && !c.IsDeleted,
            cancellationToken);

        var client = clients.FirstOrDefault();
        if (client == null)
        {
            logger.LogWarning("Authorization request with invalid client_id: {ClientId}", request.ClientId);
            return Result<Client>.Failure("unauthorized_client: Invalid client credentials");
        }

        if (!client.Enabled)
        {
            logger.LogWarning("Authorization request for disabled client: {ClientId}", request.ClientId);
            return Result<Client>.Failure("unauthorized_client: Client is disabled");
        }

        // Validate redirect URI
        var allowedUris = ParseRedirectUris(client.RedirectUris);
        if (!allowedUris.Contains(request.RedirectUri, StringComparer.Ordinal))
        {
            logger.LogWarning(
                "Invalid redirect_uri {RedirectUri} for client {ClientId}",
                request.RedirectUri,
                request.ClientId);
            return Result<Client>.Failure("invalid_request: redirect_uri not registered for this client");
        }

        // Validate grant type support
        var allowedGrantTypes = client.AllowedGrantTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(gt => gt.Trim())
            .ToList();

        if (!allowedGrantTypes.Contains("authorization_code", StringComparer.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Client {ClientId} does not support authorization_code grant type",
                request.ClientId);
            return Result<Client>.Failure("unauthorized_client: authorization_code grant not allowed");
        }

        return Result<Client>.Success(client);
    }

    /// <summary>
    /// Validates that requested scopes are valid and allowed for the client
    /// </summary>
    private async Task<Result<bool>> ValidateScopesAsync(
        string scopeString,
        long clientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scopeString))
        {
            return Result<bool>.Failure("invalid_scope: scope parameter is required");
        }

        var requestedScopes = ParseScopes(scopeString);

        // Get all valid scopes from database
        var allScopes = await scopeRepository.GetAllAsync(cancellationToken);
        var validScopeNames = allScopes.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Check if all requested scopes are valid
        var invalidScopes = requestedScopes
            .Where(scope => !validScopeNames.Contains(scope))
            .ToList();

        if (invalidScopes.Any())
        {
            logger.LogWarning(
                "Invalid scopes requested for client {ClientId}: {InvalidScopes}",
                clientId,
                string.Join(", ", invalidScopes));
            return Result<bool>.Failure($"invalid_scope: Unknown scopes: {string.Join(", ", invalidScopes)}");
        }

        var allClientValidScopes = await clientScopeRepository.GetScopesAsync(clientId, cancellationToken);
        var allowedScopeNames = allClientValidScopes
                .Select(s => s.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var notAllowedScopes = requestedScopes
            .Where(scope => !allowedScopeNames.Contains(scope))
            .ToList();
        if (notAllowedScopes.Any())
        {
            logger.LogWarning(
                "Client {ClientId} requested unauthorized scopes: {NotAllowedScopes}",
                clientId,
                string.Join(", ", notAllowedScopes));

            return Result<bool>.Failure($"invalid_scope: Client not allowed to request: {string.Join(", ", notAllowedScopes)}");
        }
        
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Parses space-separated scopes into a list
    /// </summary>
    private static List<string> ParseScopes(string scopeString)
    {
        return scopeString
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Parses comma-separated redirect URIs into a list
    /// </summary>
    private static List<string> ParseRedirectUris(string redirectUrisString)
    {
        return redirectUrisString
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(uri => uri.Trim())
            .Where(uri => !string.IsNullOrEmpty(uri))
            .ToList();
    }

    /// <summary>
    /// Builds redirect URI with authorization code and state (RFC 6749 Section 4.1.2)
    /// </summary>
    private static string BuildSuccessRedirectUri(string redirectUri, string code, string? state)
    {
        var queryParams = new Dictionary<string, string?>
        {
            ["code"] = code
        };

        if (!string.IsNullOrEmpty(state))
        {
            queryParams["state"] = state;
        }

        return QueryHelpers.AddQueryString(redirectUri, queryParams);
    }
}