namespace IdentityServer.API.Models;

public class LoginViewModel
{
    /// <summary>Username or email address</summary>
    // Nullable on purpose: MVC treats non-nullable strings as [Required], and the controller
    // shows its own error message instead of a model-validation 400.
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ReturnUrl { get; set; }

    /// <summary>Name of the application the user is signing in to (when coming from /connect/authorize)</summary>
    public string? ClientName { get; set; }

    public string? Error { get; set; }
    public bool ShowDemoAccounts { get; set; }
}

public class ConsentViewModel
{
    public string ReturnUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<ScopeViewModel> Scopes { get; set; } = [];
}

public class ScopeViewModel
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Required { get; set; }
}

public class LoggedOutViewModel
{
    public string? PostLogoutRedirectUri { get; set; }
    public string? ClientName { get; set; }
}
