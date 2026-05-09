using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Features.Profile;

namespace Cargo.CustomerService.Features.Profile.GetMyProfile;

public sealed record GetMyProfileQuery(
    string KeycloakUserId,
    string Email,
    bool EmailVerifiedInToken
) : IQuery<ProfileResponse>;