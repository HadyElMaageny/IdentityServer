namespace IdentityServer.Application.Interfaces;

public interface IUserConsentService
{
    Task<bool> HasConsentAsync(long userId, string clientId, string scopes, CancellationToken cancellationToken);
    Task GrantConsentAsync(long userId, string clientId, string scopes, CancellationToken cancellationToken);
}