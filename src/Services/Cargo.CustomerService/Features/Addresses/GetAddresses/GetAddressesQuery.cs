using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Features.Addresses;

namespace Cargo.CustomerService.Features.Addresses.GetAddresses;

public sealed record GetAddressesQuery(
    string KeycloakUserId
) : IQuery<IReadOnlyList<AddressResponse>>;