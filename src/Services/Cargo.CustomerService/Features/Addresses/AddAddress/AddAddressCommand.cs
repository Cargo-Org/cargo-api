using Cargo.BuildingBlocks.CQRS;

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