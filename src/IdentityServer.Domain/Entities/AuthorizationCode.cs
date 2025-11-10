namespace IdentityServer.Domain.Entities;

public class AuthorizationCode : BaseEntity
{
    public string Code { get; set; } = default!;
    public long UserId { get; set; }
    public long ClientId { get; set; }
    public string RedirectUri { get; set; } = default!;
    public string Scopes { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public User User { get; set; } = default!;
    public Client Client { get; set; } = default!;
}
