namespace IdentityServer.Domain.Entities;

/// <summary>
/// Represents an OAuth/OpenID Connect scope
/// </summary>
public class Scope : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool ShowInDiscoveryDocument { get; set; } = true;
}