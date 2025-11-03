using IdentityServer.Application.DTOs;
using IdentityServer.Domain.Entities;

namespace IdentityServer.Application.Interfaces;

public interface ITokenService
{
    Task<AuthResponse> GenerateTokensAsync(User user);
}