namespace IdentityServer.Domain.Entities;

public class ClientScope
{
    public long ClientId { get; set; }
    public Client Client { get; set; } = default!;

    public long ScopeId { get; set; }
    public Scope Scope { get; set; } = default!;
}