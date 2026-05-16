using Cargo.BuildingBlocks.CQRS;
using MediatR;

namespace Cargo.DriverService.Features.Profile.EnsureSocialProfile;

/// <summary>
/// Ensures a DriverProfile exists for a social-login user.
/// Called before GET /me for users who authenticated via Google etc.
/// If the profile already exists, this is a no-op.
/// </summary>
public sealed record EnsureSocialProfileCommand(
    string KeycloakUserId,
    string Email
) : ICommand<Unit>;
