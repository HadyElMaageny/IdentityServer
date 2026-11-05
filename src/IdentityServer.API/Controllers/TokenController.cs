using System.Net.Http.Headers;
using System.Text;
using FluentValidation;
using IdentityServer.API.Models;
using IdentityServer.Application.DTOs.Token;
using IdentityServer.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.API.Controllers;

/// <summary>
/// OAuth2 Token Endpoint (RFC 6749 Section 3.2)
/// </summary>
[ApiController]
[Route("connect")]
[Produces("application/json")]
[Tags("OAuth 2.0 / OpenID Connect")]
public class TokenController(
    ITokenEndpointService tokenEndpointService,
    IValidator<TokenRequest> validator,
    ILogger<TokenController> logger) : ControllerBase
{
    private static readonly string[] KnownErrorCodes =
    [
        "invalid_request", "invalid_client", "invalid_grant", "unauthorized_client",
        "unsupported_grant_type", "invalid_scope"
    ];

    /// <summary>
    /// OAuth2 Token Endpoint - exchanges authorization codes or refresh tokens for access tokens
    /// </summary>
    /// <remarks>
    /// Client authentication: HTTP Basic (client_secret_basic) or client_id / client_secret in the body
    /// (client_secret_post). Public clients send only client_id and must use PKCE (code_verifier).
    /// </remarks>
    /// <param name="form">Token request parameters (form-encoded, RFC 6749 names)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Token response or error</returns>
    /// <response code="200">Token issued successfully</response>
    /// <response code="400">Invalid request (see error response)</response>
    /// <response code="401">Client authentication failed</response>
    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Token([FromForm] TokenRequestForm form, CancellationToken cancellationToken)
    {
        // RFC 6749 §5.1: token responses must not be cached
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";

        var request = form.ToTokenRequest();
        ApplyBasicClientAuthentication(request);

        logger.LogInformation(
            "Token request received - grant_type: {GrantType}, client_id: {ClientIdentifier}",
            request.GrantType,
            request.ClientId);

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var first = validation.Errors[0].ErrorMessage;
            var code = first.StartsWith("grant_type must") ? "unsupported_grant_type" : "invalid_request";
            return BadRequest(new ErrorResponse { Error = code, ErrorDescription = first });
        }

        var result = await tokenEndpointService.ProcessTokenRequestAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Data);
        }

        var message = result.Errors?.FirstOrDefault() ?? result.Message;
        var (error, description) = MapError(message);
        var errorResponse = new ErrorResponse { Error = error, ErrorDescription = description };

        if (error == "invalid_client")
        {
            logger.LogWarning("Client authentication failed - client_id: {ClientIdentifier}", request.ClientId);
            Response.Headers.Append("WWW-Authenticate", "Basic realm=\"IdentityServer\"");
            return Unauthorized(errorResponse);
        }

        logger.LogWarning(
            "Token request failed - client_id: {ClientIdentifier}, grant_type: {GrantType}, error: {Error}",
            request.ClientId,
            request.GrantType,
            error);

        return BadRequest(errorResponse);
    }

    /// <summary>
    /// Supports client_secret_basic (RFC 6749 §2.3.1): Authorization: Basic base64(client_id:client_secret)
    /// </summary>
    private void ApplyBasicClientAuthentication(TokenRequest request)
    {
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header) ||
            !"Basic".Equals(header.Scheme, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(header.Parameter))
        {
            return;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
            var separator = decoded.IndexOf(':');
            if (separator <= 0) return;

            request.ClientId = Uri.UnescapeDataString(decoded[..separator]);
            request.ClientSecret = Uri.UnescapeDataString(decoded[(separator + 1)..]);
        }
        catch (FormatException)
        {
            // Malformed header: fall back to body credentials
        }
    }

    /// <summary>
    /// Maps service error messages to OAuth2 error codes (RFC 6749 Section 5.2)
    /// </summary>
    private static (string Error, string? Description) MapError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return ("invalid_request", null);

        // Explicit "error_code: description" from the service layer
        var idx = message.IndexOf(':');
        if (idx > 0 && KnownErrorCodes.Contains(message[..idx]))
            return (message[..idx], message[(idx + 1)..].Trim());

        var lower = message.ToLowerInvariant();
        var error = lower switch
        {
            _ when lower.Contains("client_secret") || lower.Contains("client_id") || lower.Contains("client is disabled")
                => "invalid_client",
            _ when lower.Contains("unsupported grant_type") => "unsupported_grant_type",
            _ when lower.Contains("code") || lower.Contains("refresh_token") || lower.Contains("expired")
                   || lower.Contains("redirect_uri") || lower.Contains("user not found") => "invalid_grant",
            _ when lower.Contains("scope") => "invalid_scope",
            _ when lower.Contains("internal server error") => "server_error",
            _ => "invalid_request"
        };

        return (error, message);
    }
}
