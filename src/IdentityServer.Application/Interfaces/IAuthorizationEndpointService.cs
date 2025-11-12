using IdentityServer.Application.DTOs.AuthorizationCode;
using IdentityServer.Shared.Common;

namespace IdentityServer.Application.Interfaces;

public interface IAuthorizationEndpointService
{
    Task<Result<AuthorizationCodeResponse>> ProcessAuthorizeRequestAsync(
        AuthorizationCodeRequest request,
        long userId,
        CancellationToken cancellationToken = default);
}