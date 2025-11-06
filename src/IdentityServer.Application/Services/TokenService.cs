using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var accessExpires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenMinutes);
            var refreshExpires = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays);

            var accessToken = GenerateJwtForClient(user, client, scopes, accessExpires);
            var idToken = GenerateIdTokenForClient(user, client, scopes, accessExpires);
            var refreshToken = Guid.NewGuid().ToString("N");

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
            new("client_id", client.ClientId)
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

    private string GenerateIdTokenForClient(User user, Client client, string[] scopes, DateTime expiresAt)
    {
        return GenerateJwtForClient(user, client, scopes, expiresAt);
    }

    private string GenerateClientCredentialsJwt(Client client, string[] scopes, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, client.ClientId),
            new("client_id", client.ClientId)
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
