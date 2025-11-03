namespace IdentityServer.Domain.Entities;

public class Token : BaseEntity
{
    public string AccessToken { get; set; } = string.Empty;
    public string? IdToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime AccessTokenExpiresAt { get; set; }
    public DateTime RefreshTokenExpiresAt { get; set; }
    public string TokenType { get; set; } = "Bearer";
    
    // Foreign key
    public Guid UserId { get; set; }
    
    // Navigation property
    public User User { get; set; } = null!;
}