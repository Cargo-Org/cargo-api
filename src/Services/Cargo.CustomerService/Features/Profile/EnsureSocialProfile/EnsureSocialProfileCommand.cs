using Cargo.BuildingBlocks.CQRS;
using MediatR;

namespace Cargo.CustomerService.Features.Profile.EnsureSocialProfile;

/// <summary>
/// Ensures a CustomerProfile exists for a social-login user.
/// Called before GET /me for users who authenticated via Google etc.
/// If the profile already exists, this is a no-op.
/// </summary>
public sealed record EnsureSocialProfileCommand(
    string KeycloakUserId,
    string Email
) : ICommand<Unit>;
