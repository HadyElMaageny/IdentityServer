namespace IdentityServer.Domain.Entities;

public class UserConsent : BaseEntity
{
    public long UserId { get; set; }
    public long ClientId { get; set; }
    public string Scopes { get; set; } = default!;
    public DateTime GrantedAt { get; set; }

    public User User { get; set; } = default!;
    public Client Client { get; set; } = default!;
}
