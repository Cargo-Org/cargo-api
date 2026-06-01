using FluentValidation;

namespace Cargo.DriverService.Features.Vehicles.RegisterVehicle;

public sealed class RegisterVehicleCommandValidator
    : AbstractValidator<RegisterVehicleCommand>
{
    public RegisterVehicleCommandValidator()
    {
        RuleFor(x => x.KeycloakUserId)
            .NotEmpty();

        RuleFor(x => x.VehicleNumber)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.VehicleModel)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.VehicleType)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.VehicleColor)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.ManufactureYear)
            .InclusiveBetween(1900, DateTime.UtcNow.Year + 1)
            .WithMessage("Manufacture year must be between 1900 and next year.");

        RuleFor(x => x.LicensePlate)
            .NotEmpty()
            .MaximumLength(20);
    }
}
