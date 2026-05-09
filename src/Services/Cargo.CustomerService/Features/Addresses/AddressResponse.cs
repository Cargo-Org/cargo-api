namespace Cargo.CustomerService.Features.Addresses;

public sealed record AddressResponse(
    Guid AddressId,
    string Label,
    string AddressLine,
    string City,
    string Country,
    string? PostalCode,
    double? Latitude,
    double? Longitude,
    bool IsDefault,
    DateTimeOffset CreatedAt
);