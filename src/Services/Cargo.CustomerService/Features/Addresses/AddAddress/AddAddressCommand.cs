using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Features.Addresses;

namespace Cargo.CustomerService.Features.Addresses.AddAddress;

public sealed record AddAddressCommand(
    string KeycloakUserId,
    string Label,
    string AddressLine,
    string City,
    string Country,
    string? PostalCode,
    double? Latitude,
    double? Longitude,
    bool IsDefault
) : ICommand<AddressResponse>;