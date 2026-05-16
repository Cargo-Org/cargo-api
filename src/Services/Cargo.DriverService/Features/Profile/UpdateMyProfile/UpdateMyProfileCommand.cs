using Cargo.BuildingBlocks.CQRS;

namespace Cargo.DriverService.Features.Profile.UpdateMyProfile;

public sealed record UpdateMyProfileCommand(
    string KeycloakUserId,
    string FirstName,
    string LastName,
    string PhoneNumber
) : ICommand<ProfileResponse>;
