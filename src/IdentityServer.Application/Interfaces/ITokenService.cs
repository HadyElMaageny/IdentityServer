using IdentityServer.Application.DTOs;
using IdentityServer.Application.DTOs.Token;
using IdentityServer.Domain.Entities;
using IdentityServer.Shared.Common;

namespace IdentityServer.Application.Interfaces;

public interface ITokenService
{
    Task<AuthResponse> GenerateTokensAsync(User user);
    
    /// <summary>
    /// Generates access token, refresh token, and (when the "openid" scope is granted) an ID token for a user
    /// </summary>
    Task<Result<TokenResponse>> GenerateTokensAsync(
        User user,
        Client client,
        string[] scopes,
        string? nonce = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates access token for client_credentials grant (no user context)
    /// </summary>
    Task<Result<TokenResponse>> GenerateClientTokenAsync(
        Client client,
        string[] scopes,
        CancellationToken cancellationToken = default);
}
