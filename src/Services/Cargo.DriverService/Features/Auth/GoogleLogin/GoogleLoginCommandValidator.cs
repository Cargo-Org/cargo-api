using FluentValidation;

namespace Cargo.DriverService.Features.Auth.GoogleLogin;

public sealed class GoogleLoginCommandValidator
    : AbstractValidator<GoogleLoginCommand>
{
    public GoogleLoginCommandValidator()
    {
        RuleFor(x => x.GoogleIdToken)
            .NotEmpty().WithMessage("Google ID token is required.");
    }
}
