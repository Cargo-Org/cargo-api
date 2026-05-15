using Cargo.BuildingBlocks.CQRS;

namespace Cargo.CustomerService.Features.Addresses.SetDefaultAddress;

public sealed record SetDefaultAddressCommand(
    string KeycloakUserId,
    Guid AddressId
) : ICommand<AddressResponse>;