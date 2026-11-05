namespace IdentityServer.API.Models;

public class ErrorViewModel
{
    public string Error { get; set; } = "invalid_request";
    public string? ErrorDescription { get; set; }

    /// <summary>
    /// Splits service errors formatted as "error_code: description".
    /// </summary>
    public static (string Code, string? Description) Split(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return ("invalid_request", "The request is invalid.");

        var idx = error.IndexOf(':');
        return idx > 0 && !error[..idx].Contains(' ')
            ? (error[..idx].Trim(), error[(idx + 1)..].Trim())
            : ("invalid_request", error);
    }
}
