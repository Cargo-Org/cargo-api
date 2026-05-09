using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Data;
using Cargo.CustomerService.Domain.Entities;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Features.Addresses.AddAddress;

public sealed class AddAddressCommandHandler(CustomerDbContext dbContext)
    : ICommandHandler<AddAddressCommand, AddressResponse>
{
    public async Task<ErrorOr<AddressResponse>> Handle(
        AddAddressCommand command,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.CustomerProfiles
            .FirstOrDefaultAsync(
                p => p.KeycloakUserId == command.KeycloakUserId,
                cancellationToken);

        if (profile is null)
            return Error.NotFound(
                code: "Profile.NotFound",
                description: "Customer profile not found.");

        // If this is the first address or the client requests it as default,
        // clear existing defaults first.
        if (command.IsDefault)
        {
            var existingDefaults = await dbContext.CustomerAddresses
                .Where(a => a.CustomerId == profile.Id && a.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var existing in existingDefaults)
                existing.ClearDefault();
        }

        var address = CustomerAddress.Create(
            profile.Id,
            command.Label,
            command.AddressLine,
            command.City,
            command.Country,
            command.PostalCode,
            command.Latitude,
            command.Longitude,
            command.IsDefault);

        dbContext.CustomerAddresses.Add(address);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AddressResponse(
            address.Id,
            address.Label,
            address.AddressLine,
            address.City,
            address.Country,
            address.PostalCode,
            address.Latitude,
            address.Longitude,
            address.IsDefault,
            address.CreatedAt);
    }
}