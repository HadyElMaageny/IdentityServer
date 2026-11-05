using IdentityServer.Application.DTOs.Token;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.API.Models;

/// <summary>
/// Form body of the token endpoint, using the RFC 6749 parameter names (application/x-www-form-urlencoded)
/// </summary>
public class TokenRequestForm
{
    /// <summary>authorization_code, refresh_token or client_credentials</summary>
    [FromForm(Name = "grant_type")] public string? GrantType { get; set; }

    /// <summary>Authorization code (authorization_code grant)</summary>
    [FromForm(Name = "code")] public string? Code { get; set; }

    /// <summary>Redirect URI used in the authorization request (authorization_code grant)</summary>
    [FromForm(Name = "redirect_uri")] public string? RedirectUri { get; set; }

    /// <summary>PKCE code verifier (authorization_code grant, public clients)</summary>
    [FromForm(Name = "code_verifier")] public string? CodeVerifier { get; set; }

    /// <summary>Refresh token (refresh_token grant)</summary>
    [FromForm(Name = "refresh_token")] public string? RefreshToken { get; set; }

    /// <summary>Client identifier (or use HTTP Basic authentication)</summary>
    [FromForm(Name = "client_id")] public string? ClientId { get; set; }

    /// <summary>Client secret for confidential clients (or use HTTP Basic authentication)</summary>
    [FromForm(Name = "client_secret")] public string? ClientSecret { get; set; }

    /// <summary>Requested scopes, space-separated (client_credentials grant)</summary>
    [FromForm(Name = "scope")] public string? Scope { get; set; }

    public TokenRequest ToTokenRequest() => new()
    {
        GrantType = GrantType ?? string.Empty,
        Code = Code,
        RedirectUri = RedirectUri,
        CodeVerifier = CodeVerifier,
        RefreshToken = RefreshToken,
        ClientId = ClientId ?? string.Empty,
        ClientSecret = ClientSecret,
        Scope = Scope
    };
}
