using FluentValidation;

namespace Cargo.DriverService.Features.Vehicles.GetLicenseUploadUrl;

public sealed class GetLicenseUploadUrlQueryValidator
    : AbstractValidator<GetLicenseUploadUrlQuery>
{
    public GetLicenseUploadUrlQueryValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty();

        RuleFor(x => x.ContentType)
            .NotEmpty();

        RuleFor(x => x.KeycloakUserId)
            .NotEmpty();
    }
}
