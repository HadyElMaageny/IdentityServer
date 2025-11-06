namespace IdentityServer.Application.DTOs.Token;

/// <summary>
/// OAuth2 token request as per RFC 6749
/// </summary>
public class TokenRequest
{
    /// <summary>
    /// Grant type: authorization_code, refresh_token, client_credentials
    /// </summary>
    public string GrantType { get; set; } = string.Empty;

    /// <summary>
    /// Authorization code (required for authorization_code grant)
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Redirect URI used in authorization request (required for authorization_code)
    /// </summary>
    public string? RedirectUri { get; set; }

    /// <summary>
    /// Refresh token (required for refresh_token grant)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Client ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret (required for confidential clients)
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Requested scopes (space-separated)
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// PKCE code verifier (for public clients)
    /// </summary>
    public string? CodeVerifier { get; set; }
}
