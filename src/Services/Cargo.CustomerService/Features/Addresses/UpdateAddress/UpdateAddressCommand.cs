using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Features.Addresses;

namespace Cargo.CustomerService.Features.Addresses.UpdateAddress;

public sealed record UpdateAddressCommand(
    string KeycloakUserId,
    Guid AddressId,
    string Label,
    string AddressLine,
    string City,
    string Country,
    string? PostalCode,
    double? Latitude,
    double? Longitude
) : ICommand<AddressResponse>;