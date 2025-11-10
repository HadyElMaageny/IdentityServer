namespace IdentityServer.Application.DTOs.AuthorizationCode;

public class AuthorizationCodeRequest
{
    public string ClientId { get; set; } =  string.Empty;
    public string RedirectUri { get; set; } =  string.Empty;
    public string ResponseType { get; set; } =  string.Empty;
    public string Scope { get; set; } =  string.Empty;
    public string State { get; set; } =  string.Empty;
    
}