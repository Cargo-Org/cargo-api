using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Data;
using ErrorOr;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Features.Addresses.SetDefaultAddress;

public sealed class SetDefaultAddressCommandHandler(CustomerDbContext dbContext)
    : ICommandHandler<SetDefaultAddressCommand, AddressResponse>
{
    public async Task<ErrorOr<AddressResponse>> Handle(
        SetDefaultAddressCommand command,
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

        // Verify the target address exists and belongs to this customer.
        var target = await dbContext.CustomerAddresses
            .FirstOrDefaultAsync(
                a => a.Id == command.AddressId &&
                     a.CustomerId == profile.Id,
                cancellationToken);

        if (target is null)
            return Error.NotFound(
                code: "Address.NotFound",
                description: "Address not found.");

        // Load all addresses for this customer to clear existing defaults.
        // This is the same tracked DbContext — all changes go into one transaction.
        var allAddresses = await dbContext.CustomerAddresses
            .Where(a => a.CustomerId == profile.Id)
            .ToListAsync(cancellationToken);

        // Clear every address first, then set the target.
        // Single SaveChangesAsync call = single database transaction.
        foreach (var address in allAddresses)
            address.ClearDefault();

        target.SetAsDefault();

        // EF Core wraps all pending UPDATE statements in one transaction.
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AddressResponse(
            target.Id,
            target.Label,
            target.AddressLine,
            target.City,
            target.Country,
            target.PostalCode,
            target.Latitude,
            target.Longitude,
            target.IsDefault,
            target.CreatedAt);
    }
}