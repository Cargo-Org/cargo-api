using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Data;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Features.Addresses.GetAddresses;

public sealed class GetAddressesQueryHandler(CustomerDbContext dbContext)
    : IQueryHandler<GetAddressesQuery, IReadOnlyList<AddressResponse>>
{
    public async Task<ErrorOr<IReadOnlyList<AddressResponse>>> Handle(
        GetAddressesQuery query,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.KeycloakUserId == query.KeycloakUserId,
                cancellationToken);

        if (profile is null)
            return Error.NotFound(
                code: "Profile.NotFound",
                description: "Customer profile not found.");

        var addresses = await dbContext.CustomerAddresses
            .AsNoTracking()
            .Where(a => a.CustomerId == profile.Id)
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.CreatedAt)
            .Select(a => new AddressResponse(
                a.Id,
                a.Label,
                a.AddressLine,
                a.City,
                a.Country,
                a.PostalCode,
                a.Latitude,
                a.Longitude,
                a.IsDefault,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        return addresses;
    }
}