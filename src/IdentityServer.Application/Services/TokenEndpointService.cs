using IdentityServer.Application.DTOs.Token;
using IdentityServer.Application.Interfaces;
using IdentityServer.Domain.Entities;
using IdentityServer.Domain.Interfaces;
using IdentityServer.Shared.Common;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace IdentityServer.Application.Services;

public class TokenEndpointService : ITokenEndpointService
{
    private readonly IRepository<Client> _clientRepository;
    private readonly IRepository<AuthorizationCode> _authCodeRepository;
    private readonly IRepository<Token> _tokenRepository;
    private readonly IRepository<User> _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TokenEndpointService> _logger;

    public TokenEndpointService(
        IRepository<Client> clientRepository,
        IRepository<AuthorizationCode> authCodeRepository,
        IRepository<Token> tokenRepository,
        IRepository<User> userRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        ILogger<TokenEndpointService> logger)
    {
        _clientRepository = clientRepository;
        _authCodeRepository = authCodeRepository;
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<TokenResponse>> ProcessTokenRequestAsync(TokenRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var clientResult = await ValidateClientAsync(request, cancellationToken);
            if (!clientResult.IsSuccess)
            {
                _logger.LogWarning("Client validation failed for client_id: {ClientId}", request.ClientId);
                return Result<TokenResponse>.Failure(clientResult.Errors ?? new List<string> { "Invalid client" });
            }

            var client = clientResult.Data!;

            return request.GrantType?.ToLowerInvariant() switch
            {
                "authorization_code" => await ProcessAuthorizationCodeGrantAsync(request, client, cancellationToken),
                "refresh_token" => await ProcessRefreshTokenGrantAsync(request, client, cancellationToken),
                "client_credentials" => await ProcessClientCredentialsGrantAsync(request, client, cancellationToken),
                _ => Result<TokenResponse>.Failure($"Unsupported grant_type: {request.GrantType}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing token request for client: {ClientId}", request.ClientId);
            return Result<TokenResponse>.Failure("Internal server error processing token request");
        }
    }

    private async Task<Result<Client>> ValidateClientAsync(TokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
            return Result<Client>.Failure("client_id is required");

        var clients =
            await _clientRepository.FindAsync(c => c.ClientId == request.ClientId && !c.IsDeleted, cancellationToken);
        var client = clients.FirstOrDefault();

        if (client == null)
            return Result<Client>.Failure("Invalid client_id");

        if (client.RequireClientSecret)
        {
            if (string.IsNullOrWhiteSpace(request.ClientSecret))
                return Result<Client>.Failure("client_secret is required for this client");

            if (!SecureCompare(client.ClientSecret, request.ClientSecret))
                return Result<Client>.Failure("Invalid client_secret");
        }

        if (!client.Enabled)
            return Result<Client>.Failure("Client is disabled");

        return Result<Client>.Success(client);
    }

    private async Task<Result<TokenResponse>> ProcessAuthorizationCodeGrantAsync(TokenRequest request, Client client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return Result<TokenResponse>.Failure("code is required for authorization_code grant");

        if (string.IsNullOrWhiteSpace(request.RedirectUri))
            return Result<TokenResponse>.Failure("redirect_uri is required for authorization_code grant");

        var authCodes =
            await _authCodeRepository.FindAsync(ac => ac.Code == request.Code && !ac.IsDeleted, cancellationToken);
        var authCode = authCodes.FirstOrDefault();

        if (authCode == null)
        {
            _logger.LogWarning("Invalid authorization code attempted");
            return Result<TokenResponse>.Failure("Invalid authorization code");
        }

        if (authCode.IsUsed)
        {
            _logger.LogWarning("Authorization code reuse detected", request.Code);
            return Result<TokenResponse>.Failure("Authorization code has already been used");
        }

        if (authCode.ExpiresAt < DateTime.UtcNow)
            return Result<TokenResponse>.Failure("Authorization code has expired");

        if (authCode.ClientId != client.Id)
        {
            _logger.LogWarning("Authorization code client mismatch");
            return Result<TokenResponse>.Failure("Invalid authorization code for this client");
        }

        if (!authCode.RedirectUri.Equals(request.RedirectUri, StringComparison.Ordinal))
        {
            _logger.LogWarning("Redirect URI mismatch in token request");
            return Result<TokenResponse>.Failure("redirect_uri does not match authorization request");
        }

        if (!string.IsNullOrEmpty(authCode.CodeChallenge))
        {
            if (string.IsNullOrWhiteSpace(request.CodeVerifier))
                return Result<TokenResponse>.Failure("code_verifier is required (PKCE)");

            if (!ValidatePkce(request.CodeVerifier, authCode.CodeChallenge, authCode.CodeChallengeMethod))
            {
                _logger.LogWarning("PKCE validation failed");
                return Result<TokenResponse>.Failure("Invalid code_verifier");
            }
        }

        var user = await _userRepository.GetByIdAsync(authCode.UserId, cancellationToken);
        if (user == null || !user.IsActive)
            return Result<TokenResponse>.Failure("User not found or inactive");

        authCode.IsUsed = true;
        authCode.UpdatedAt = DateTime.UtcNow;
        await _authCodeRepository.UpdateAsync(authCode, cancellationToken);

        var tokenResult = await _tokenService.GenerateTokensAsync(user, client,
            authCode.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries), cancellationToken);

        if (!tokenResult.IsSuccess)
            return Result<TokenResponse>.Failure(tokenResult.Errors ?? new List<string>
                { "Failed to generate tokens" });

        var tokens = tokenResult.Data!;

        if (!string.IsNullOrEmpty(tokens.RefreshToken))
        {
            var refreshTokenEntity = new Token
            {
                TokenValue = tokens.RefreshToken!,
                TokenType = "refresh_token",
                UserId = user.Id,
                ClientId = client.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                Scopes = authCode.Scopes,
                CreatedAt = DateTime.UtcNow
            };

            await _tokenRepository.AddAsync(refreshTokenEntity, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Access token issued for user {UserId} via authorization_code grant", user.Id);

        return Result<TokenResponse>.Success(tokens);
    }

    private async Task<Result<TokenResponse>> ProcessRefreshTokenGrantAsync(TokenRequest request, Client client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result<TokenResponse>.Failure("refresh_token is required");

        var tokens = await _tokenRepository.FindAsync(
            t => t.TokenValue == request.RefreshToken && t.TokenType == "refresh_token" && !t.IsDeleted && !t.IsRevoked,
            cancellationToken);
        var refreshToken = tokens.FirstOrDefault();

        if (refreshToken == null)
            return Result<TokenResponse>.Failure("Invalid refresh_token");

        if (refreshToken.ExpiresAt < DateTime.UtcNow)
            return Result<TokenResponse>.Failure("Refresh token has expired");

        if (refreshToken.ClientId != client.Id)
        {
            _logger.LogWarning("Refresh token client mismatch");
            return Result<TokenResponse>.Failure("Invalid refresh_token for this client");
        }

        var user = await _userRepository.GetByIdAsync(refreshToken.UserId, cancellationToken);
        if (user == null || !user.IsActive)
            return Result<TokenResponse>.Failure("User not found or inactive");

        var scopes = refreshToken.Scopes?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        var tokenResult = await _tokenService.GenerateTokensAsync(user, client, scopes, cancellationToken);

        if (!tokenResult.IsSuccess)
            return Result<TokenResponse>.Failure(tokenResult.Errors ?? new List<string>
                { "Failed to generate tokens" });

        var newTokens = tokenResult.Data!;

        refreshToken.IsRevoked = true;
        refreshToken.UpdatedAt = DateTime.UtcNow;
        await _tokenRepository.UpdateAsync(refreshToken, cancellationToken);

        if (!string.IsNullOrEmpty(newTokens.RefreshToken))
        {
            var newRefreshToken = new Token
            {
                TokenValue = newTokens.RefreshToken!,
                TokenType = "refresh_token",
                UserId = user.Id,
                ClientId = client.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                Scopes = refreshToken.Scopes,
                CreatedAt = DateTime.UtcNow
            };

            await _tokenRepository.AddAsync(newRefreshToken, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Access token refreshed for user {UserId}", user.Id);

        return Result<TokenResponse>.Success(newTokens);
    }

    private async Task<Result<TokenResponse>> ProcessClientCredentialsGrantAsync(TokenRequest request, Client client,
        CancellationToken cancellationToken)
    {
        var scopes = request.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        var tokenResult = await _tokenService.GenerateClientTokenAsync(client, scopes, cancellationToken);

        if (!tokenResult.IsSuccess)
            return Result<TokenResponse>.Failure(tokenResult.Errors ?? new List<string> { "Failed to generate token" });

        _logger.LogInformation("Client credentials token issued for client {ClientId}", client.ClientId);

        return tokenResult;
    }

    private bool ValidatePkce(string codeVerifier, string codeChallenge, string? method)
    {
        method ??= "plain";

        if (method.Equals("plain", StringComparison.OrdinalIgnoreCase))
            return codeVerifier.Equals(codeChallenge, StringComparison.Ordinal);

        if (method.Equals("S256", StringComparison.OrdinalIgnoreCase))
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(System.Text.Encoding.ASCII.GetBytes(codeVerifier));
            var computedChallenge = Base64UrlEncode(hash);
            return computedChallenge.Equals(codeChallenge, StringComparison.Ordinal);
        }

        return false;
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool SecureCompare(string a, string b)
    {
        if (a == null || b == null || a.Length != b.Length)
            return false;

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }
}