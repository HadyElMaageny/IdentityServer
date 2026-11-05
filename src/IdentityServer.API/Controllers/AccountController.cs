using System.Security.Claims;
using IdentityServer.API.Extensions;
using IdentityServer.API.Models;
using IdentityServer.Domain.Entities;
using IdentityServer.Domain.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityServer.API.Controllers;

/// <summary>
/// Server-rendered sign-in / sign-out pages. Establishes the identity server's own session cookie,
/// which /connect/authorize uses to know who the user is.
/// </summary>
[Route("")]
[ApiExplorerSettings(IgnoreApi = true)]
public class AccountController(
    IRepository<User> userRepository,
    IRepository<Client> clientRepository,
    IPasswordHasher<User> passwordHasher,
    IWebHostEnvironment environment,
    ILogger<AccountController> logger) : Controller
{
    [HttpGet("account/login")]
    public async Task<IActionResult> Login([FromQuery(Name = "ReturnUrl")] string? returnUrl, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated == true && IsSafeReturnUrl(returnUrl))
            return LocalRedirect(returnUrl!);

        return View(await BuildLoginModelAsync(new LoginViewModel { ReturnUrl = returnUrl }, ct));
    }

    [HttpPost("account/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] LoginViewModel model, CancellationToken ct)
    {
        var password = model.Password ?? string.Empty;
        var login = model.Username?.Trim() ?? string.Empty;

        var user = string.IsNullOrEmpty(login)
            ? null
            : (await userRepository.FindAsync(
                u => (u.Username == login || u.Email == login) && !u.IsDeleted, ct)).FirstOrDefault();

        var valid = user is { IsActive: true } &&
                    passwordHasher.VerifyHashedPassword(user, user.Password, password)
                        != PasswordVerificationResult.Failed;

        if (!valid)
        {
            logger.LogWarning("Failed sign-in attempt for {Login}", login);
            model.Error = "Invalid username or password.";
            model.Password = string.Empty;
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return View(await BuildLoginModelAsync(model, ct));
        }

        var claims = new List<Claim>
        {
            new("sub", user!.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("email", user.Email),
            new("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, ServiceExtensions.CookieScheme));
        await HttpContext.SignInAsync(ServiceExtensions.CookieScheme, principal);

        logger.LogInformation("User {UserId} signed in", user.Id);

        return IsSafeReturnUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl!) : Redirect("/");
    }

    /// <summary>
    /// OpenID Connect RP-initiated logout: ends the session and returns to a registered post-logout URI.
    /// </summary>
    [HttpGet("connect/endsession")]
    [HttpGet("account/logout")]
    public async Task<IActionResult> Logout(
        [FromQuery(Name = "client_id")] string? clientId,
        [FromQuery(Name = "post_logout_redirect_uri")] string? postLogoutRedirectUri,
        CancellationToken ct)
    {
        await HttpContext.SignOutAsync(ServiceExtensions.CookieScheme);

        var model = new LoggedOutViewModel();

        if (!string.IsNullOrEmpty(clientId))
        {
            var client = (await clientRepository.FindAsync(
                c => c.ClientIdentifier == clientId && !c.IsDeleted, ct)).FirstOrDefault();

            model.ClientName = client?.ClientName;

            var allowed = (client?.PostLogoutRedirectUris ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Only redirect to URIs the client registered (prevents open redirects)
            if (!string.IsNullOrEmpty(postLogoutRedirectUri) &&
                allowed.Contains(postLogoutRedirectUri, StringComparer.Ordinal))
            {
                return Redirect(postLogoutRedirectUri);
            }
        }

        return View("LoggedOut", model);
    }

    private async Task<LoginViewModel> BuildLoginModelAsync(LoginViewModel model, CancellationToken ct)
    {
        model.ShowDemoAccounts = environment.IsDevelopment();

        // When coming from /connect/authorize, show which application the user is signing in to
        if (IsSafeReturnUrl(model.ReturnUrl))
        {
            var query = QueryHelpers.ParseQuery(new Uri(new Uri("https://local"), model.ReturnUrl!).Query);
            if (query.TryGetValue("client_id", out var clientId))
            {
                var id = clientId.ToString();
                var client = (await clientRepository.FindAsync(
                    c => c.ClientIdentifier == id && !c.IsDeleted, ct)).FirstOrDefault();
                model.ClientName = client?.ClientName;
            }
        }

        return model;
    }

    private bool IsSafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl);
}
