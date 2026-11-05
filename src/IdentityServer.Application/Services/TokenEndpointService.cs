using IdentityServer.Application.DTOs.Token;
using IdentityServer.Application.Interfaces;
using IdentityServer.Domain.Entities;
using IdentityServer.Domain.Interfaces;
using IdentityServer.Shared.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace IdentityServer.Application.Services;

/// <summary>
/// OAuth 2.0 token endpoint logic (RFC 6749 §3.2, RFC 7636 PKCE)
/// </summary>
public class TokenEndpointService(
    IRepository<Client> clientRepository,
    IRepository<AuthorizationCode> authCodeRepository,
    IRepository<Token> tokenRepository,
    IRepository<User> userRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    IPasswordHasher<User> secretHasher,
    ILogger<TokenEndpointService> logger)
    : ITokenEndpointService
{
    private const int RefreshTokenDays = 30;

    public async Task<Result<TokenResponse>> ProcessTokenRequestAsync(TokenRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var clientResult = await ValidateClientAsync(request, cancellationToken);
            if (!clientResult.IsSuccess)
            {
                logger.LogWarning("Client validation failed for client_id: {ClientIdentifier}", request.ClientId);
                return Result<TokenResponse>.Failure(clientResult.Errors ?? ["Invalid client"]);
            }

            var client = clientResult.Data!;
            var grantType = request.GrantType?.ToLowerInvariant();

            if (!IsGrantAllowed(client, grantType))
            {
                logger.LogWarning("Client {ClientId} attempted disallowed grant_type {GrantType}",
                    client.ClientIdentifier, grantType);
                return Result<TokenResponse>.Failure($"unauthorized_client: grant_type '{grantType}' is not allowed for this client");
            }

            return grantType switch
            {
                "authorization_code" => await ProcessAuthorizationCodeGrantAsync(request, client, cancellationToken),
                "refresh_token" => await ProcessRefreshTokenGrantAsync(request, client, cancellationToken),
                "client_credentials" => await ProcessClientCredentialsGrantAsync(request, client, cancellationToken),
                _ => Result<TokenResponse>.Failure($"Unsupported grant_type: {request.GrantType}")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing token request for client: {ClientIdentifier}", request.ClientId);
            return Result<TokenResponse>.Failure("Internal server error processing token request");
        }
    }

    private async Task<Result<Client>> ValidateClientAsync(TokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
            return Result<Client>.Failure("client_id is required");

        var client = (await clientRepository.FindAsync(
                c => c.ClientIdentifier == request.ClientId && !c.IsDeleted, cancellationToken))
            .FirstOrDefault();

        if (client == null)
            return Result<Client>.Failure("Invalid client_id");

        if (!client.Enabled)
            return Result<Client>.Failure("Client is disabled");

        if (client.RequireClientSecret)
        {
            if (string.IsNullOrWhiteSpace(request.ClientSecret))
                return Result<Client>.Failure("client_secret is required for this client");

            // Client secrets are stored hashed (PBKDF2), exactly like user passwords
            var verification = secretHasher.VerifyHashedPassword(new User(), client.ClientSecret, request.ClientSecret);
            if (verification == PasswordVerificationResult.Failed)
                return Result<Client>.Failure("Invalid client_secret");
        }

        return Result<Client>.Success(client);
    }

    private static bool IsGrantAllowed(Client client, string? grantType)
    {
        if (string.IsNullOrEmpty(grantType))
            return false;

        return client.AllowedGrantTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(grantType, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Result<TokenResponse>> ProcessAuthorizationCodeGrantAsync(TokenRequest request, Client client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return Result<TokenResponse>.Failure("code is required for authorization_code grant");

        if (string.IsNullOrWhiteSpace(request.RedirectUri))
            return Result<TokenResponse>.Failure("redirect_uri is required for authorization_code grant");

        var authCode = (await authCodeRepository.FindAsync(
                ac => ac.Code == request.Code && !ac.IsDeleted, cancellationToken))
            .FirstOrDefault();

        if (authCode == null)
        {
            logger.LogWarning("Invalid authorization code attempted");
            return Result<TokenResponse>.Failure("Invalid authorization code");
        }

        if (authCode.IsUsed)
        {
            logger.LogWarning("Authorization code reuse detected for client {ClientId}", client.ClientIdentifier);
            return Result<TokenResponse>.Failure("Authorization code has already been used");
        }

        if (authCode.ExpiresAt < DateTime.UtcNow)
            return Result<TokenResponse>.Failure("Authorization code has expired");

        if (authCode.ClientId != client.Id)
        {
            logger.LogWarning("Authorization code client mismatch");
            return Result<TokenResponse>.Failure("Invalid authorization code for this client");
        }

        if (!authCode.RedirectUri.Equals(request.RedirectUri, StringComparison.Ordinal))
        {
            logger.LogWarning("Redirect URI mismatch in token request");
            return Result<TokenResponse>.Failure("redirect_uri does not match authorization request");
        }

        // PKCE (RFC 7636 §4.6): if a challenge was sent at /authorize, the verifier is mandatory here
        if (!string.IsNullOrEmpty(authCode.CodeChallenge))
        {
            if (string.IsNullOrWhiteSpace(request.CodeVerifier))
                return Result<TokenResponse>.Failure("code_verifier is required (PKCE)");

            if (!ValidatePkce(request.CodeVerifier, authCode.CodeChallenge, authCode.CodeChallengeMethod))
            {
                logger.LogWarning("PKCE verification failed for client {ClientId}", client.ClientIdentifier);
                return Result<TokenResponse>.Failure("Invalid code_verifier (PKCE verification failed)");
            }
        }

        var user = await userRepository.GetByIdAsync(authCode.UserId, cancellationToken);
        if (user == null || !user.IsActive)
            return Result<TokenResponse>.Failure("User not found or inactive");

        // One-time use: burn the code before issuing tokens
        authCode.IsUsed = true;
        authCode.UpdatedAt = DateTime.UtcNow;
        await authCodeRepository.UpdateAsync(authCode, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var scopes = authCode.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tokenResult = await tokenService.GenerateTokensAsync(user, client, scopes, authCode.Nonce, cancellationToken);

        if (!tokenResult.IsSuccess)
            return Result<TokenResponse>.Failure(tokenResult.Errors ?? ["Failed to generate tokens"]);

        var tokens = tokenResult.Data!;
        await StoreOrDropRefreshTokenAsync(tokens, user, client, authCode.Scopes, cancellationToken);

        logger.LogInformation("Access token issued for user {UserId} via authorization_code grant", user.Id);

        return Result<TokenResponse>.Success(tokens);
    }

    private async Task<Result<TokenResponse>> ProcessRefreshTokenGrantAsync(TokenRequest request, Client client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result<TokenResponse>.Failure("refresh_token is required");

        var refreshToken = (await tokenRepository.FindAsync(
                t => t.TokenValue == request.RefreshToken && t.TokenType == "refresh_token" && !t.IsDeleted && !t.IsRevoked,
                cancellationToken))
            .FirstOrDefault();

        if (refreshToken == null)
            return Result<TokenResponse>.Failure("Invalid refresh_token");

        if (refreshToken.ExpiresAt < DateTime.UtcNow)
            return Result<TokenResponse>.Failure("Refresh token has expired");

        if (refreshToken.ClientId != client.Id)
        {
            logger.LogWarning("Refresh token client mismatch");
            return Result<TokenResponse>.Failure("Invalid refresh_token for this client");
        }

        var user = await userRepository.GetByIdAsync(refreshToken.UserId, cancellationToken);
        if (user == null || !user.IsActive)
            return Result<TokenResponse>.Failure("User not found or inactive");

        var scopes = refreshToken.Scopes?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var tokenResult = await tokenService.GenerateTokensAsync(user, client, scopes, null, cancellationToken);

        if (!tokenResult.IsSuccess)
            return Result<TokenResponse>.Failure(tokenResult.Errors ?? ["Failed to generate tokens"]);

        var newTokens = tokenResult.Data!;

        // Refresh token rotation: the old token can never be used again
        refreshToken.IsRevoked = true;
        refreshToken.UpdatedAt = DateTime.UtcNow;
        await tokenRepository.UpdateAsync(refreshToken, cancellationToken);

        await StoreOrDropRefreshTokenAsync(newTokens, user, client, refreshToken.Scopes, cancellationToken);

        logger.LogInformation("Access token refreshed for user {UserId}", user.Id);

        return Result<TokenResponse>.Success(newTokens);
    }

    private async Task<Result<TokenResponse>> ProcessClientCredentialsGrantAsync(TokenRequest request, Client client,
        CancellationToken cancellationToken)
    {
        if (!client.RequireClientSecret)
            return Result<TokenResponse>.Failure("unauthorized_client: client_credentials requires a confidential client");

        var scopes = request.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var tokenResult = await tokenService.GenerateClientTokenAsync(client, scopes, cancellationToken);

        if (!tokenResult.IsSuccess)
            return Result<TokenResponse>.Failure(tokenResult.Errors ?? ["Failed to generate token"]);

        logger.LogInformation("Client credentials token issued for client {ClientIdentifier}", client.ClientIdentifier);

        return tokenResult;
    }

    /// <summary>
    /// Persists the refresh token if the client is allowed to use one; otherwise removes it from the response.
    /// Always commits pending changes.
    /// </summary>
    private async Task StoreOrDropRefreshTokenAsync(TokenResponse tokens, User user, Client client, string? scopes,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(tokens.RefreshToken) && IsGrantAllowed(client, "refresh_token"))
        {
            await tokenRepository.AddAsync(new Token
            {
                TokenValue = tokens.RefreshToken!,
                TokenType = "refresh_token",
                UserId = user.Id,
                ClientId = client.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenDays),
                Scopes = scopes,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        else
        {
            tokens.RefreshToken = null;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static bool ValidatePkce(string codeVerifier, string codeChallenge, string? method)
    {
        method ??= "plain";

        string computed;
        if (method.Equals("S256", StringComparison.Ordinal))
        {
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            computed = Base64UrlEncode(hash);
        }
        else if (method.Equals("plain", StringComparison.Ordinal))
        {
            computed = codeVerifier;
        }
        else
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(codeChallenge));
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
