namespace IdentityServer.Application.Interfaces;

public interface IUserConsentService
{
    Task<bool> HasConsentAsync(long userId, long clientId, string scopes, CancellationToken cancellationToken);
    Task GrantConsentAsync(long userId, long clientId, string scopes, CancellationToken cancellationToken);
}