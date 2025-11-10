namespace IdentityServer.Domain.Entities;

public class Client : BaseEntity
{
    public string ClientIdentifier { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;
    public string ClientName { get; set; } = default!;
    public string RedirectUris { get; set; } = default!; // comma-separated list
    public string? PostLogoutRedirectUris { get; set; }
    public string AllowedGrantTypes { get; set; } = "authorization_code";
    public bool AllowOfflineAccess { get; set; } = false; // For refresh tokens
    public bool Enabled { get; set; } = true;
    public bool RequireClientSecret { get; set; } = true;

    public ICollection<ClientScope> ClientScopes { get; set; } = new List<ClientScope>();
    public ICollection<UserConsent> UserConsents { get; set; } = new List<UserConsent>();
    public ICollection<AuthorizationCode> AuthorizationCodes { get; set; } = new List<AuthorizationCode>();
}
