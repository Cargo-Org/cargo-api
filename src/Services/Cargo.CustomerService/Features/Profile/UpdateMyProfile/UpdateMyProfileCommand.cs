using Cargo.BuildingBlocks.CQRS;

namespace Cargo.CustomerService.Features.Profile.UpdateMyProfile;

public sealed record UpdateMyProfileCommand(
    string KeycloakUserId,
    string FullName,
    string PhoneNumber
) : ICommand<ProfileResponse>;