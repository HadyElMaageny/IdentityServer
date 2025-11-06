namespace IdentityServer.Domain.Entities;

public class Scope : BaseEntity
{
    public string Name { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? Description { get; set; }
    public bool Required { get; set; } = false;
    public bool Emphasize { get; set; } = false;

    public ICollection<ClientScope> ClientScopes { get; set; } = new List<ClientScope>();
}