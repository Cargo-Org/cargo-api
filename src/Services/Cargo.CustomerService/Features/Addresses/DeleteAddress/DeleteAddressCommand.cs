using Cargo.BuildingBlocks.CQRS;
using MediatR;

namespace Cargo.CustomerService.Features.Addresses.DeleteAddress;

// Returns Unit — no payload needed on successful delete.
public sealed record DeleteAddressCommand(
    string KeycloakUserId,
    Guid AddressId
) : ICommand;