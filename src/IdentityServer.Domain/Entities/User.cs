namespace IdentityServer.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    // Navigation property for related tokens
    public ICollection<Token> Tokens { get; set; } = new List<Token>();
}