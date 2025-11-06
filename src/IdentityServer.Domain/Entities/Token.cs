namespace IdentityServer.Domain.Entities;

public class Token : BaseEntity
{
    public string TokenValue { get; set; } = default!;
    public string TokenType { get; set; } = default!; // "refresh_token", "access_token"
    public long UserId { get; set; }
    public long ClientId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public string? Scopes { get; set; }

    public User User { get; set; } = default!;
    public Client Client { get; set; } = default!;
}
