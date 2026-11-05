using System.Security.Claims;
using IdentityServer.API.Extensions;
using IdentityServer.API.Models;
using IdentityServer.Application.DTOs.AuthorizationCode;
using IdentityServer.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.API.Controllers;

/// <summary>
/// OAuth 2.0 Authorization Endpoint (RFC 6749 Section 3.1)
/// </summary>
/// <remarks>
/// This is a browser endpoint: the client application redirects the user here.
/// <list type="number">
/// <item>No sign-in session yet: redirect to the login page, which returns here afterwards.</item>
/// <item>Consent missing: redirect to the consent page, which returns here afterwards.</item>
/// <item>Otherwise: redirect back to the client's redirect_uri with <c>code</c> and <c>state</c>.</item>
/// </list>
/// </remarks>
[ApiController]
[Route("connect")]
[Tags("OAuth 2.0 / OpenID Connect")]
public class AuthorizationController(
    IAuthorizationEndpointService authorizationEndpointService,
    ILogger<AuthorizationController> logger) : Controller
{
    /// <summary>
    /// OAuth 2.0 / OpenID Connect authorization request (Authorization Code flow with PKCE)
    /// </summary>
    /// <param name="clientId">Registered client identifier</param>
    /// <param name="redirectUri">Registered redirect URI (exact match)</param>
    /// <param name="responseType">Must be "code"</param>
    /// <param name="scope">Space-separated scopes, e.g. "openid profile email"</param>
    /// <param name="state">Opaque value echoed back to the client (CSRF protection)</param>
    /// <param name="codeChallenge">PKCE code challenge (required for public clients)</param>
    /// <param name="codeChallengeMethod">"S256" (recommended) or "plain"</param>
    /// <param name="nonce">OIDC nonce, echoed in the id_token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="302">Redirect to login, consent, or back to the client</response>
    [HttpGet("authorize")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> Authorize(
        [FromQuery(Name = "client_id")] string? clientId,
        [FromQuery(Name = "redirect_uri")] string? redirectUri,
        [FromQuery(Name = "response_type")] string? responseType,
        [FromQuery(Name = "scope")] string? scope,
        [FromQuery(Name = "state")] string? state,
        [FromQuery(Name = "code_challenge")] string? codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod,
        [FromQuery(Name = "nonce")] string? nonce,
        CancellationToken cancellationToken)
    {
        var request = new AuthorizationCodeRequest
        {
            ClientId = clientId ?? string.Empty,
            RedirectUri = redirectUri ?? string.Empty,
            ResponseType = responseType ?? string.Empty,
            Scope = scope ?? string.Empty,
            State = state ?? string.Empty,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            Nonce = nonce
        };

        logger.LogInformation("Authorization request received for client: {ClientId}", request.ClientId);

        // 1. Never redirect to an unverified redirect_uri: show an error page instead
        var clientCheck = await authorizationEndpointService.ValidateClientAndRedirectUriAsync(request, cancellationToken);
        if (!clientCheck.IsSuccess)
        {
            return ErrorPage(clientCheck.Errors?.FirstOrDefault());
        }

        // 2. Ask the user to sign in (the cookie handler redirects to /account/login?ReturnUrl=...)
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge(ServiceExtensions.CookieScheme);
        }

        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
        {
            logger.LogWarning("Signed-in session has no valid subject claim; forcing re-login");
            await HttpContext.SignOutAsync(ServiceExtensions.CookieScheme);
            return Challenge(ServiceExtensions.CookieScheme);
        }

        // 3. Validate the rest of the request, check consent, issue the code
        var result = await authorizationEndpointService.ProcessAuthorizeRequestAsync(request, userId, cancellationToken);

        if (!result.IsSuccess)
        {
            return ErrorPage(result.Errors?.FirstOrDefault());
        }

        var response = result.Data!;

        return response.Action switch
        {
            "consent" => RedirectToAction(nameof(ConsentController.Index), "Consent",
                new { returnUrl = Request.Path + Request.QueryString }),
            "redirect" or "error" => Redirect(response.RedirectUri!),
            _ => ErrorPage("server_error: Unexpected response action")
        };
    }

    private ViewResult ErrorPage(string? error)
    {
        var (code, description) = ErrorViewModel.Split(error);
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View("Error", new ErrorViewModel { Error = code, ErrorDescription = description });
    }
}
