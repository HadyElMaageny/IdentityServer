using System.Text.Json.Serialization;

namespace IdentityServer.Application.DTOs.Token;

/// <summary>
/// OAuth2 error response as per RFC 6749 Section 5.2
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error code (invalid_request, invalid_client, invalid_grant, etc.)
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error description
    /// </summary>
    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }

    /// <summary>
    /// URI with error information
    /// </summary>
    [JsonPropertyName("error_uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorUri { get; set; }
}
