using IdentityServer.Application.DTOs.AuthorizationCode;
using IdentityServer.Domain.Entities;
using IdentityServer.Shared.Common;

namespace IdentityServer.Application.Interfaces;

public interface IAuthorizationEndpointService
{
    Task<Result<AuthorizationCodeResponse>> ProcessAuthorizeRequestAsync(
        AuthorizationCodeRequest request,
        long userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates client_id and redirect_uri. Only after this succeeds is it safe to redirect the browser to redirect_uri.
    /// </summary>
    Task<Result<Client>> ValidateClientAndRedirectUriAsync(
        AuthorizationCodeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds an RFC 6749 §4.1.2.1 error redirect (error, error_description, state).
    /// </summary>
    string BuildErrorRedirectUri(string redirectUri, string error, string description, string? state);
}
