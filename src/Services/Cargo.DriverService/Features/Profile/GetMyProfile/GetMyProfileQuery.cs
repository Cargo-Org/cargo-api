using Cargo.BuildingBlocks.CQRS;

namespace Cargo.DriverService.Features.Profile.GetMyProfile;

public sealed record GetMyProfileQuery(
    string KeycloakUserId,
    string Email,
    bool EmailVerifiedInToken
) : IQuery<ProfileResponse>;
