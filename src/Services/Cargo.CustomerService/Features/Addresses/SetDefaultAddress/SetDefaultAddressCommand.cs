using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Features.Addresses;

namespace Cargo.CustomerService.Features.Addresses.SetDefaultAddress;

public sealed record SetDefaultAddressCommand(
    string KeycloakUserId,
    Guid AddressId
) : ICommand<AddressResponse>;