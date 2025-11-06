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

    // PKCE support (RFC 7636)
    public string? CodeChallenge { get; set; }
    public string? CodeChallengeMethod { get; set; } // "plain" or "S256"

    public User User { get; set; } = default!;
    public Client Client { get; set; } = default!;
}
