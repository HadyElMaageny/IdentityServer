namespace IdentityServer.Domain.Entities;

/// <summary>
/// Represents an OAuth 2.0 / OpenID Connect client application
/// </summary>
public class Client : BaseEntity
{
    // Client Identity
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Client URIs
    public string? ClientUri { get; set; }
    public string? LogoUri { get; set; }
    public string RedirectUris { get; set; } = string.Empty; // JSON array or comma-separated
    public string? PostLogoutRedirectUris { get; set; } // JSON array or comma-separated
    
    // Client Configuration
    public string AllowedGrantTypes { get; set; } = string.Empty; // JSON array or comma-separated
    public string AllowedScopes { get; set; } = string.Empty; // JSON array or comma-separated
    public string ClientType { get; set; } = "confidential"; // confidential, public, spa
    
    // Token Settings
    public int AccessTokenLifetime { get; set; } = 3600; // 1 hour in seconds
    public int RefreshTokenLifetime { get; set; } = 2592000; // 30 days in seconds
    public bool AllowOfflineAccess { get; set; } = false; // Refresh token support
    
    // Security & Behavior
    public bool RequireClientSecret { get; set; } = true;
    public bool RequireConsent { get; set; } = true;
    public bool RequirePkce { get; set; } = true; // Proof Key for Code Exchange
    public bool AllowPlainTextPkce { get; set; } = false;
    
    // Status
    public bool IsEnabled { get; set; } = true;
}