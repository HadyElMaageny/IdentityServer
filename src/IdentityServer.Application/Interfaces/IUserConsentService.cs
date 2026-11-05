namespace IdentityServer.Application.Interfaces;

public interface IUserConsentService
{
    Task<bool> HasConsentAsync(long userId, long clientId, string scopes, CancellationToken cancellationToken);

    /// <summary>
    /// Records consent for the given scopes, merging them into any consent the user already granted to the client.
    /// </summary>
    Task GrantConsentAsync(long userId, long clientId, string scopes, CancellationToken cancellationToken);
}
