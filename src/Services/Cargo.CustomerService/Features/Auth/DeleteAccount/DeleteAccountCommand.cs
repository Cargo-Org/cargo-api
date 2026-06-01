using Cargo.BuildingBlocks.CQRS;
using MediatR;

namespace Cargo.CustomerService.Features.Auth.DeleteAccount;

public sealed record DeleteAccountCommand(
    string KeycloakUserId
) : ICommand<Unit>;
