using IdentityServer.Application.DTOs.AuthorizationCode;
using IdentityServer.Application.DTOs.Token;
using IdentityServer.Shared.Common;

namespace IdentityServer.Application.Interfaces;

/// <summary>
/// Service for handling OAuth2 token endpoint operations
/// </summary>
public interface ITokenEndpointService
{
    /// <summary>
    /// Processes OAuth2 token requests for all grant types
    /// </summary>
    /// <param name="request">Token request parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing token response or error</returns>
    Task<Result<TokenResponse>> ProcessTokenRequestAsync(
        TokenRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AuthorizationCodeResponse>> ProcessAuthorizationCodeGrantAsync(AuthorizationCodeRequest request, long userId,
        CancellationToken cancellationToken = default);
}