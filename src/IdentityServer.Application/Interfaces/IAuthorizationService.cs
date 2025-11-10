using System.Security.Claims;
using IdentityServer.Domain.Entities;
using IdentityServer.Shared.Common;

namespace IdentityServer.Application.Interfaces;

public interface IAuthorizationService
{
    Task<Result<AuthorizationCode>> GenerateAuthorizationCodeAsync(long userId, long clientId, string[] scopes,
        string redirectUri, CancellationToken cancellationToken);
}