using IdentityServer.Application.DTOs;
using IdentityServer.Application.Interfaces;
using IdentityServer.Domain.Entities;

namespace IdentityServer.Application.Services;

public class JwtSettings
{
    public string Issuer { get; set; } = "your-identity-server";
    public string Audience { get; set; } = "your-audience";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 30;
}

public class TokenService : ITokenService
{
    public Task<AuthResponse> GenerateTokensAsync(User user)
    {
        throw new NotImplementedException();
    }
}