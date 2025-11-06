namespace IdentityServer.Application.DTOs.Token;

/// <summary>
/// OAuth2 token response as per RFC 6749
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// Access token (JWT)
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Token type (always "Bearer" for JWT)
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Token expiration in seconds
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Refresh token (for obtaining new access tokens)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Granted scopes (space-separated)
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// ID token (for OpenID Connect)
    /// </summary>
    public string? IdToken { get; set; }
}
