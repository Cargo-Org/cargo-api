using Cargo.BuildingBlocks.CQRS;

namespace Cargo.DriverService.Features.Auth.RequestEmailVerification;

public record RequestEmailVerificationCommand(string KeycloakUserId) : ICommand;
