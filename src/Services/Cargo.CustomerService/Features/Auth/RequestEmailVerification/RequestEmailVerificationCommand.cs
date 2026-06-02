using Cargo.BuildingBlocks.CQRS;

namespace Cargo.CustomerService.Features.Auth.RequestEmailVerification;

public record RequestEmailVerificationCommand(string KeycloakUserId) : ICommand;
