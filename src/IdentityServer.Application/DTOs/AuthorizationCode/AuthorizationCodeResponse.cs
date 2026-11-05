namespace IdentityServer.Application.DTOs.AuthorizationCode;

/// <summary>
/// Outcome of an authorization request.
/// Action is one of: "redirect" (send the browser to RedirectUri), "consent" (show the consent screen)
/// or "error" (send the browser to RedirectUri, which carries error parameters).
/// </summary>
public class AuthorizationCodeResponse
{
    public string Action { get; set; } = string.Empty;
    public string? RedirectUri { get; set; }
    public string? ClientName { get; set; }
    public List<string>? Scopes { get; set; }
    public string? State { get; set; }
}
