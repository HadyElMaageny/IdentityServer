using IdentityServer.API.Extensions;
using IdentityServer.Domain.Entities;
using IdentityServer.Domain.Interfaces;
using IdentityServer.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IdentityServer.API.Controllers;

/// <summary>
/// OpenID Connect discovery and UserInfo endpoints
/// </summary>
[ApiController]
[Produces("application/json")]
[Tags("OAuth 2.0 / OpenID Connect")]
public class OidcController(
    IRepository<User> userRepository,
    IRepository<Scope> scopeRepository,
    IOptions<JwtSettings> jwtOptions) : ControllerBase
{
    /// <summary>
    /// OpenID Connect discovery document (OpenID Connect Discovery 1.0)
    /// </summary>
    [HttpGet("/.well-known/openid-configuration")]
    [AllowAnonymous]
    public async Task<IActionResult> Discovery(CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var scopes = (await scopeRepository.GetAllAsync(ct)).Select(s => s.Name).ToArray();

        return Ok(new Dictionary<string, object>
        {
            ["issuer"] = jwtOptions.Value.Issuer,
            ["authorization_endpoint"] = $"{baseUrl}/connect/authorize",
            ["token_endpoint"] = $"{baseUrl}/connect/token",
            ["userinfo_endpoint"] = $"{baseUrl}/connect/userinfo",
            ["end_session_endpoint"] = $"{baseUrl}/connect/endsession",
            ["scopes_supported"] = scopes,
            ["response_types_supported"] = new[] { "code" },
            ["grant_types_supported"] = new[] { "authorization_code", "refresh_token", "client_credentials" },
            ["code_challenge_methods_supported"] = new[] { "S256", "plain" },
            ["token_endpoint_auth_methods_supported"] = new[] { "client_secret_basic", "client_secret_post", "none" },
            ["subject_types_supported"] = new[] { "public" },
            ["id_token_signing_alg_values_supported"] = new[] { "HS256" },
            ["claims_supported"] = new[] { "sub", "name", "preferred_username", "email", "email_verified", "nonce", "auth_time" }
        });
    }

    /// <summary>
    /// UserInfo endpoint (OIDC Core §5.3): returns claims about the user, filtered by the token's scopes
    /// </summary>
    /// <response code="200">Claims about the authenticated user</response>
    /// <response code="401">Missing or invalid access token</response>
    [HttpGet("/connect/userinfo")]
    [Authorize(AuthenticationSchemes = ServiceExtensions.JwtScheme)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UserInfo(CancellationToken ct)
    {
        var sub = User.FindFirst("sub")?.Value;
        var scopes = User.FindAll("scope").Select(c => c.Value).ToHashSet();

        if (!scopes.Contains("openid") || !long.TryParse(sub, out var userId))
            return Forbid(ServiceExtensions.JwtScheme);

        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
            return Unauthorized();

        var claims = new Dictionary<string, object> { ["sub"] = sub! };

        if (scopes.Contains("profile"))
        {
            claims["name"] = user.Username;
            claims["preferred_username"] = user.Username;
        }

        if (scopes.Contains("email"))
        {
            claims["email"] = user.Email;
            claims["email_verified"] = false;
        }

        return Ok(claims);
    }
}
