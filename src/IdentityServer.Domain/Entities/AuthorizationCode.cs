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

    /// <summary>PKCE code challenge (RFC 7636)</summary>
    public string? CodeChallenge { get; set; }

    /// <summary>PKCE code challenge method: "S256" or "plain"</summary>
    public string? CodeChallengeMethod { get; set; }

    /// <summary>OpenID Connect nonce, echoed back in the id_token</summary>
    public string? Nonce { get; set; }

    public User User { get; set; } = default!;
    public Client Client { get; set; } = default!;
}
