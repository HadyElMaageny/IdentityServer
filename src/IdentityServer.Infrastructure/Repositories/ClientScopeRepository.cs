using IdentityServer.Domain.Entities;
using IdentityServer.Domain.Interfaces;
using IdentityServer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Infrastructure.Repositories;

public class ClientScopeRepository(ApplicationDbContext context) : IClientScopeRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<IEnumerable<Scope>> GetScopesAsync(long clientId, CancellationToken cancellationToken)
    {
        return await _context.ClientScopes
            .Where(c => c.ClientId == clientId)
            .Select(c => c.Scope)
            .ToListAsync(cancellationToken);
    }
}