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
public class TokenController : ControllerBase
{
    private readonly ITokenEndpointService _tokenEndpointService;
    private readonly ILogger<TokenController> _logger;

    public TokenController(
        ITokenEndpointService tokenEndpointService,
        ILogger<TokenController> logger)
    {
        _tokenEndpointService = tokenEndpointService;
        _logger = logger;
    }

    /// <summary>
    /// OAuth2 Token Endpoint - exchanges authorization codes or refresh tokens for access tokens
    /// </summary>
    /// <param name="request">Token request parameters (typically form-encoded)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Token response or error</returns>
    /// <response code="200">Token issued successfully</response>
    /// <response code="400">Invalid request (see error response)</response>
    /// <response code="401">Client authentication failed</response>
    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TokenErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(TokenErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Token(
        [FromForm] TokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Log token request (without sensitive data)
            _logger.LogInformation(
                "Token request received - grant_type: {GrantType}, client_id: {ClientId}",
                request.GrantType,
                request.ClientId);

            // Process token request through service layer
            var result = await _tokenEndpointService.ProcessTokenRequestAsync(request, cancellationToken);

            if (result.IsSuccess)
            {
                // RFC 6749: Success response with 200 OK
                return Ok(result.Data);
            }

            // Determine appropriate error response
            var errorResponse = new TokenErrorResponse
            {
                Error = DetermineErrorCode(result.Message),
                ErrorDescription = result.Message
            };

            // Client authentication failures return 401
            if (IsClientAuthError(errorResponse.Error))
            {
                _logger.LogWarning(
                    "Client authentication failed - client_id: {ClientId}, error: {Error}",
                    request.ClientId,
                    errorResponse.Error);

                Response.Headers.Append("WWW-Authenticate", "Basic realm=\"IdentityServer\"");
                return Unauthorized(errorResponse);
            }

            // Other errors return 400
            _logger.LogWarning(
                "Token request failed - client_id: {ClientId}, grant_type: {GrantType}, error: {Error}",
                request.ClientId,
                request.GrantType,
                errorResponse.Error);

            return BadRequest(errorResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in token endpoint");

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new TokenErrorResponse
                {
                    Error = "server_error",
                    ErrorDescription = "An internal server error occurred"
                });
        }
    }

    /// <summary>
    /// Maps error messages to OAuth2 error codes (RFC 6749 Section 5.2)
    /// </summary>
    private static string DetermineErrorCode(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "invalid_request";

        var lowerMessage = message.ToLowerInvariant();

        return lowerMessage switch
        {
            _ when lowerMessage.Contains("client") && lowerMessage.Contains("secret") => "invalid_client",
            _ when lowerMessage.Contains("client") => "invalid_client",
            _ when lowerMessage.Contains("code") => "invalid_grant",
            _ when lowerMessage.Contains("refresh_token") => "invalid_grant",
            _ when lowerMessage.Contains("expired") => "invalid_grant",
            _ when lowerMessage.Contains("grant_type") => "unsupported_grant_type",
            _ when lowerMessage.Contains("scope") => "invalid_scope",
            _ => "invalid_request"
        };
    }

    /// <summary>
    /// Determines if error is related to client authentication
    /// </summary>
    private static bool IsClientAuthError(string errorCode)
    {
        return errorCode == "invalid_client";
    }
}
