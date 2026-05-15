using Cargo.BuildingBlocks.CQRS;

namespace Cargo.CustomerService.Features.Addresses.GetAddresses;

public sealed record GetAddressesQuery(
    string KeycloakUserId
) : IQuery<IReadOnlyList<AddressResponse>>;