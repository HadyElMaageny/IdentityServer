using System.Security.Claims;
using IdentityServer.API.Models;
using IdentityServer.Application.DTOs.AuthorizationCode;
using IdentityServer.Application.Interfaces;
using IdentityServer.Domain.Entities;
using IdentityServer.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityServer.API.Controllers;

/// <summary>
/// User consent screen: lets the signed-in user approve or deny the scopes an application asks for.
/// </summary>
[Route("connect/consent")]
[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
public class ConsentController(
    IAuthorizationEndpointService authorizationEndpointService,
    IUserConsentService userConsentService,
    IClientScopeRepository clientScopeRepository,
    IRepository<Scope> scopeRepository,
    ILogger<ConsentController> logger) : Controller
{
    private const string AuthorizePath = "/connect/authorize";

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? returnUrl, CancellationToken ct)
    {
        var context = await LoadAsync(returnUrl, ct);
        if (context.Error is not null)
            return context.Error;

        var requested = ParseScopes(context.Request!.Scope);
        var scopes = (await scopeRepository.FindAsync(s => requested.Contains(s.Name), ct))
            .OrderByDescending(s => s.Required)
            .ThenBy(s => requested.IndexOf(s.Name))
            .Select(s => new ScopeViewModel
            {
                Name = s.Name,
                DisplayName = s.DisplayName,
                Description = s.Description,
                Required = s.Required
            })
            .ToList();

        return View(new ConsentViewModel
        {
            ReturnUrl = returnUrl!,
            ClientId = context.Client!.ClientIdentifier,
            ClientName = context.Client.ClientName,
            RedirectUri = context.Request.RedirectUri,
            Username = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            Email = User.FindFirstValue("email") ?? string.Empty,
            Scopes = scopes
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([FromForm] string? returnUrl, [FromForm] string? decision, CancellationToken ct)
    {
        var context = await LoadAsync(returnUrl, ct);
        if (context.Error is not null)
            return context.Error;

        var request = context.Request!;
        var client = context.Client!;
        var userId = long.Parse(User.FindFirstValue("sub")!);

        if (!string.Equals(decision, "allow", StringComparison.Ordinal))
        {
            logger.LogInformation("User {UserId} denied consent for client {ClientId}", userId, client.ClientIdentifier);
            return Redirect(authorizationEndpointService.BuildErrorRedirectUri(
                request.RedirectUri, "access_denied", "The user denied the request", request.State));
        }

        // Only record scopes this client is actually allowed to request
        var allowed = (await clientScopeRepository.GetScopesAsync(client.Id, ct))
            .Select(s => s.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var granted = ParseScopes(request.Scope).Where(allowed.Contains);

        await userConsentService.GrantConsentAsync(userId, client.Id, string.Join(' ', granted), ct);

        // Back to /connect/authorize, which now finds the consent and issues the code
        return LocalRedirect(returnUrl!);
    }

    private async Task<(AuthorizationCodeRequest? Request, Client? Client, IActionResult? Error)> LoadAsync(
        string? returnUrl, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl) ||
            !returnUrl.StartsWith(AuthorizePath + "?", StringComparison.Ordinal))
        {
            return (null, null, ErrorPage("invalid_request", "Missing or invalid return URL."));
        }

        var query = QueryHelpers.ParseQuery(new Uri(new Uri("https://local"), returnUrl).Query);
        var request = new AuthorizationCodeRequest
        {
            ClientId = query["client_id"].ToString(),
            RedirectUri = query["redirect_uri"].ToString(),
            ResponseType = query["response_type"].ToString(),
            Scope = query["scope"].ToString(),
            State = query["state"].ToString()
        };

        var clientResult = await authorizationEndpointService.ValidateClientAndRedirectUriAsync(request, ct);
        if (!clientResult.IsSuccess)
        {
            var (code, description) = ErrorViewModel.Split(clientResult.Errors?.FirstOrDefault());
            return (null, null, ErrorPage(code, description));
        }

        return (request, clientResult.Data, null);
    }

    private ViewResult ErrorPage(string code, string? description)
    {
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View("Error", new ErrorViewModel { Error = code, ErrorDescription = description });
    }

    private static List<string> ParseScopes(string scope) =>
        scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
}
