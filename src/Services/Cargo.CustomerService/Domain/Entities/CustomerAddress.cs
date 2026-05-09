namespace Cargo.CustomerService.Domain.Entities;

public sealed class CustomerAddress
{
    private CustomerAddress() { }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }

    public string Label { get; private set; } = null!;
    public string AddressLine { get; private set; } = null!;
    public string City { get; private set; } = null!;

    // ISO 3166-1 alpha-2 code — e.g. EG, US, GB.
    public string Country { get; private set; } = null!;

    // Nullable — not all countries use postal codes.
    public string? PostalCode { get; private set; }

    // Nullable — provided by mobile if GPS available at save time.
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    // Only one address per customer can be true.
    // Setting a new default must clear all others in a single transaction.
    // See SetDefaultAddressCommandHandler — enforced at the handler level.
    public bool IsDefault { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    // Back-reference to the owning profile. EF Core populates this.
    public CustomerProfile Customer { get; private set; } = null!;

    public static CustomerAddress Create(
        Guid customerId,
        string label,
        string addressLine,
        string city,
        string country,
        string? postalCode,
        double? latitude,
        double? longitude,
        bool isDefault)
    {
        return new CustomerAddress
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Label = label,
            AddressLine = addressLine,
            City = city,
            Country = country,
            PostalCode = postalCode,
            Latitude = latitude,
            Longitude = longitude,
            IsDefault = isDefault,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(
        string label,
        string addressLine,
        string city,
        string country,
        string? postalCode,
        double? latitude,
        double? longitude)
    {
        Label = label;
        AddressLine = addressLine;
        City = city;
        Country = country;
        PostalCode = postalCode;
        Latitude = latitude;
        Longitude = longitude;
    }

    public void SetAsDefault() => IsDefault = true;
    public void ClearDefault() => IsDefault = false;
}