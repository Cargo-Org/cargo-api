using FluentValidation;

namespace Cargo.DriverService.Features.Profile.UpdateMyProfile;

public sealed class UpdateMyProfileCommandValidator
    : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(255);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(255);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+[1-9]\d{1,14}$")
            .WithMessage("Phone number must be in E.164 format (e.g. +201012345678).");
    }
}
