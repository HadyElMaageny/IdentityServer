namespace IdentityServer.Application.DTOs.AuthorizationCode;

public class AuthorizationCodeResponse
{
    public string Action { get; set; } = string.Empty;
    public string? RedirectUri { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public List<string>? Scopes { get; set; }
    public string? State { get; set; }
}