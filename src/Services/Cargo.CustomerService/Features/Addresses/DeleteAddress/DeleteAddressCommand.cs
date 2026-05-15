using Cargo.BuildingBlocks.CQRS;

namespace Cargo.CustomerService.Features.Addresses.DeleteAddress;

// Returns Unit — no payload needed on successful delete.
public sealed record DeleteAddressCommand(
    string KeycloakUserId,
    Guid AddressId
) : ICommand;