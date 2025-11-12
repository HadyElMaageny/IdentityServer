using IdentityServer.Domain.Entities;

namespace IdentityServer.Domain.Interfaces;

public interface IClientScopeRepository
{
    Task<IEnumerable<Scope>> GetScopesAsync(long clientId, CancellationToken cancellationToken);
}