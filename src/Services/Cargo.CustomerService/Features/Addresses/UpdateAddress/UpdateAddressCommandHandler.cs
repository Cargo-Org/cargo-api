using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Data;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Features.Addresses.UpdateAddress;

public sealed class UpdateAddressCommandHandler(CustomerDbContext dbContext)
    : ICommandHandler<UpdateAddressCommand, AddressResponse>
{
    public async Task<ErrorOr<AddressResponse>> Handle(
        UpdateAddressCommand command,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.KeycloakUserId == command.KeycloakUserId,
                cancellationToken);

        if (profile is null)
            return Error.NotFound(
                code: "Profile.NotFound",
                description: "Customer profile not found.");

        // Filter by both ID and CustomerId — ownership verified in one query.
        var address = await dbContext.CustomerAddresses
            .FirstOrDefaultAsync(
                a => a.Id == command.AddressId &&
                     a.CustomerId == profile.Id,
                cancellationToken);

        if (address is null)
            return Error.NotFound(
                code: "Address.NotFound",
                description: "Address not found.");

        address.Update(
            command.Label,
            command.AddressLine,
            command.City,
            command.Country,
            command.PostalCode,
            command.Latitude,
            command.Longitude);

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