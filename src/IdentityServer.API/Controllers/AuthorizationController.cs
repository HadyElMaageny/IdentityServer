using IdentityServer.Application.DTOs.AuthorizationCode;
using IdentityServer.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IdentityServer.API.Controllers;

/// <summary>
/// OAuth 2.0 Authorization Endpoint (RFC 6749 Section 3.1)
/// </summary>
[ApiController]
[Route("connect")]
[Produces("application/json")]
public class AuthorizationController : ControllerBase
{
    private readonly IAuthorizationEndpointService _authorizationEndpointService;
    private readonly ILogger<AuthorizationController> _logger;

    public AuthorizationController(
        IAuthorizationEndpointService authorizationEndpointService,
        ILogger<AuthorizationController> logger)
    {
        _authorizationEndpointService = authorizationEndpointService;
        _logger = logger;
    }

    /// <summary>
    /// OAuth 2.0 Authorization Endpoint
    /// Handles authorization code flow requests (RFC 6749 Section 4.1.1)
    /// </summary>
    /// <param name="request">Authorization request parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authorization response or error</returns>
    [HttpGet("authorize")]
    [HttpPost("authorize")]
    [Authorize]
    public async Task<IActionResult> Authorize([FromQuery] AuthorizationCodeRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Authorization request received for client: {ClientId}", request.ClientId);

        // Extract user ID from claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
        {
            _logger.LogWarning("Invalid or missing user ID claim in authenticated request");
            return BuildErrorResponse("server_error", "Invalid user session", request.RedirectUri, request.State);
        }

        var result = await _authorizationEndpointService.ProcessAuthorizeRequestAsync(request, userId, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Authorization request failed for client {ClientId}: {Errors}",
                request.ClientId,
                string.Join(", ", result.Errors ?? new List<string> { "Unknown error" }));

            var error = result.Errors?.FirstOrDefault() ?? "invalid_request";
            var errorDescription = result.Message ?? "The authorization request is invalid";

            return BuildErrorResponse(error, errorDescription, request.RedirectUri, request.State);
        }

        var response = result.Data!;

        // Handle different response actions
        return response.Action switch
        {
            "redirect" => Ok(response),
            "consent" => Ok(response),
            _ => BuildErrorResponse("server_error", "Unexpected response action", request.RedirectUri, request.State)
        };
    }

    /// <summary>
    /// Builds an OAuth 2.0 error response (RFC 6749 Section 4.1.2.1)
    /// </summary>
    private IActionResult BuildErrorResponse(string error, string errorDescription, string? redirectUri, string? state)
    {
        var errorResponse = new
        {
            error,
            error_description = errorDescription,
            state
        };

        // If we have a valid redirect URI, we should redirect with error
        // Otherwise, return the error as JSON
        if (!string.IsNullOrEmpty(redirectUri) && Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
        {
            return Ok(new AuthorizationCodeResponse
            {
                Action = "error",
                RedirectUri = BuildErrorRedirectUri(redirectUri, error, errorDescription, state),
                State = state
            });
        }

        return BadRequest(errorResponse);
    }

    /// <summary>
    /// Builds redirect URI with error parameters
    /// </summary>
    private static string BuildErrorRedirectUri(string redirectUri, string error, string errorDescription, string? state)
    {
        var uriBuilder = new UriBuilder(redirectUri);
        var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);

        query["error"] = error;
        query["error_description"] = errorDescription;

        if (!string.IsNullOrEmpty(state))
        {
            query["state"] = state;
        }

        uriBuilder.Query = query.ToString();
        return uriBuilder.ToString();
    }
}