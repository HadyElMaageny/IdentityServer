namespace IdentityServer.Application.DTOs.AuthorizationCode;

/// <summary>
/// OAuth 2.0 / OpenID Connect authorization request (RFC 6749 §4.1.1, RFC 7636, OIDC Core §3.1.2.1)
/// </summary>
public class AuthorizationCodeRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string ResponseType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;

    /// <summary>PKCE code challenge (RFC 7636)</summary>
    public string? CodeChallenge { get; set; }

    /// <summary>PKCE code challenge method: "S256" (recommended) or "plain"</summary>
    public string? CodeChallengeMethod { get; set; }

    /// <summary>OIDC nonce, bound into the issued id_token</summary>
    public string? Nonce { get; set; }
}
