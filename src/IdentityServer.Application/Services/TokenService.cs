using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IdentityServer.Application.DTOs;
using IdentityServer.Application.Interfaces;
using IdentityServer.Domain.Entities;
using IdentityServer.Domain.Interfaces;
using IdentityServer.Shared.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace IdentityServer.Application.Services;

public class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly IRepository<Token> _tokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TokenService(
        IOptions<JwtSettings> jwtOptions,
        IRepository<Token> tokenRepository,
        IUnitOfWork unitOfWork)
    {
        _jwtSettings = jwtOptions.Value;
        _tokenRepository = tokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResponse> GenerateTokensAsync(User user)
    {
        var accessExpires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenMinutes);
        var refreshExpires = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays);

        var accessToken = GenerateJwt(user, accessExpires);
        var idToken = GenerateIdToken(user, accessExpires);
        var refreshToken = Guid.NewGuid().ToString("N");


        return new AuthResponse
        {
            AccessToken = accessToken,
            IdToken = idToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = _jwtSettings.AccessTokenMinutes * 60
        };
    }

    private string GenerateJwt(User user, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new("username", user.Username),
            new("email", user.Email ?? string.Empty)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateIdToken(User user, DateTime expiresAt)
    {
        // For now, reuse the same JWT as IdToken (Phase 1)
        return GenerateJwt(user, expiresAt);
    }
    public async Task<Shared.Common.Result<DTOs.Token.TokenResponse>> GenerateTokensAsync(
        User user,
        Client client,
        string[] scopes,
        string? nonce = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var accessExpires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenMinutes);

            var accessToken = GenerateJwtForClient(user, client, scopes, accessExpires);
            var idToken = scopes.Contains("openid")
                ? GenerateIdTokenForClient(user, client, scopes, nonce, accessExpires)
                : null;
            var refreshToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

            return Shared.Common.Result<DTOs.Token.TokenResponse>.Success(new DTOs.Token.TokenResponse
            {
                AccessToken = accessToken,
                TokenType = "Bearer",
                ExpiresIn = _jwtSettings.AccessTokenMinutes * 60,
                RefreshToken = refreshToken,
                Scope = string.Join(" ", scopes),
                IdToken = idToken
            });
        }
        catch (Exception ex)
        {
            return Shared.Common.Result<DTOs.Token.TokenResponse>.Failure($"Error generating tokens: {ex.Message}");
        }
    }

    public async Task<Shared.Common.Result<DTOs.Token.TokenResponse>> GenerateClientTokenAsync(
        Client client,
        string[] scopes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var accessExpires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenMinutes);

            var accessToken = GenerateClientCredentialsJwt(client, scopes, accessExpires);

            return await Task.FromResult(Shared.Common.Result<DTOs.Token.TokenResponse>.Success(new DTOs.Token.TokenResponse
            {
                AccessToken = accessToken,
                TokenType = "Bearer",
                ExpiresIn = _jwtSettings.AccessTokenMinutes * 60,
                Scope = string.Join(" ", scopes)
            }));
        }
        catch (Exception ex)
        {
            return Shared.Common.Result<DTOs.Token.TokenResponse>.Failure($"Error generating client token: {ex.Message}");
        }
    }

    private string GenerateJwtForClient(User user, Client client, string[] scopes, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new("username", user.Username),
            new("email", user.Email ?? string.Empty),
            new("client_id", client.ClientIdentifier)
        };

        foreach (var scope in scopes)
        {
            claims.Add(new Claim("scope", scope));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// OpenID Connect ID token (OIDC Core §2): audience is the client, claims depend on granted scopes.
    /// </summary>
    private string GenerateIdTokenForClient(User user, Client client, string[] scopes, string? nonce, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.AuthTime, now, ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Iat, now, ClaimValueTypes.Integer64)
        };

        if (!string.IsNullOrEmpty(nonce))
            claims.Add(new Claim(JwtRegisteredClaimNames.Nonce, nonce));

        if (scopes.Contains("profile"))
        {
            claims.Add(new Claim("name", user.Username));
            claims.Add(new Claim("preferred_username", user.Username));
        }

        if (scopes.Contains("email"))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
            claims.Add(new Claim("email_verified", "false", ClaimValueTypes.Boolean));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: client.ClientIdentifier,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateClientCredentialsJwt(Client client, string[] scopes, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, client.ClientIdentifier),
            new("client_id", client.ClientIdentifier)
        };

        foreach (var scope in scopes)
        {
            claims.Add(new Claim("scope", scope));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
