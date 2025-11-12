using FluentValidation;
using IdentityServer.Application.DTOs.AuthorizationCode;

namespace IdentityServer.Application.Validators;

/// <summary>
/// Validator for OAuth 2.0 authorization code requests (RFC 6749)
/// </summary>
public class AuthorizationCodeRequestValidator : AbstractValidator<AuthorizationCodeRequest>
{
    public AuthorizationCodeRequestValidator()
    {
        RuleFor(x => x.ClientId)
            .NotEmpty()
            .WithMessage("client_id is required")
            .MaximumLength(256)
            .WithMessage("client_id must not exceed 256 characters");

        RuleFor(x => x.RedirectUri)
            .NotEmpty()
            .WithMessage("redirect_uri is required")
            .Must(BeAValidUri)
            .WithMessage("redirect_uri must be a valid URI");

        RuleFor(x => x.ResponseType)
            .NotEmpty()
            .WithMessage("response_type is required")
            .Must(x => x == "code")
            .WithMessage("response_type must be 'code' for authorization code flow");

        RuleFor(x => x.Scope)
            .NotEmpty()
            .WithMessage("scope is required")
            .MaximumLength(1000)
            .WithMessage("scope must not exceed 1000 characters");

        RuleFor(x => x.State)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.State))
            .WithMessage("state must not exceed 500 characters");
    }

    private bool BeAValidUri(string uri)
    {
        return Uri.TryCreate(uri, UriKind.Absolute, out var result)
               && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
}
