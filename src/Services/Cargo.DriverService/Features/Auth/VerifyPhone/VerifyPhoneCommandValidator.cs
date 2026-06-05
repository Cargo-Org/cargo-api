using FluentValidation;

namespace Cargo.DriverService.Features.Auth.VerifyPhone;

public sealed class VerifyPhoneCommandValidator : AbstractValidator<VerifyPhoneCommand>
{
    public VerifyPhoneCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters.");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("Verification code is required.")
            .Matches(@"^\d{5}$").WithMessage("Verification code must be exactly 5 digits.");
    }
}
