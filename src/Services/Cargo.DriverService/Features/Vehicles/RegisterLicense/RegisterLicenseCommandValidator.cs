using FluentValidation;

namespace Cargo.DriverService.Features.Vehicles.RegisterLicense;

public sealed class RegisterLicenseCommandValidator
    : AbstractValidator<RegisterLicenseCommand>
{
    private const long MaxFileSizeBytes = 15 * 1024 * 1024; // 15 MB

    public RegisterLicenseCommandValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty();

        RuleFor(x => x.KeycloakUserId)
            .NotEmpty();

        RuleFor(x => x.ObjectKey)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.OriginalFileName)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"File size must not exceed {MaxFileSizeBytes / (1024 * 1024)} MB.");
    }
}
