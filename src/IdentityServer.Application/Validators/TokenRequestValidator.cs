using FluentValidation;
using IdentityServer.Application.DTOs.Token;

namespace IdentityServer.Application.Validators;

/// <summary>
/// Validates OAuth2 token requests
/// </summary>
public class TokenRequestValidator : AbstractValidator<TokenRequest>
{
    public TokenRequestValidator()
    {
        RuleFor(x => x.GrantType)
            .NotEmpty()
            .WithMessage("grant_type is required")
            .Must(gt => new[] { "authorization_code", "refresh_token", "client_credentials" }
                .Contains(gt?.ToLowerInvariant()))
            .WithMessage("grant_type must be authorization_code, refresh_token, or client_credentials");

        RuleFor(x => x.ClientId)
            .NotEmpty()
            .WithMessage("client_id is required")
            .MaximumLength(100);

        // Authorization code grant specific rules
        When(x => x.GrantType?.ToLowerInvariant() == "authorization_code", () =>
        {
            RuleFor(x => x.Code)
                .NotEmpty()
                .WithMessage("code is required for authorization_code grant");

            RuleFor(x => x.RedirectUri)
                .NotEmpty()
                .WithMessage("redirect_uri is required for authorization_code grant")
                .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
                .WithMessage("redirect_uri must be a valid absolute URI");
        });

        // Refresh token grant specific rules
        When(x => x.GrantType?.ToLowerInvariant() == "refresh_token", () =>
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty()
                .WithMessage("refresh_token is required for refresh_token grant");
        });
    }
}
