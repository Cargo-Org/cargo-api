using Cargo.BuildingBlocks.CQRS;
using Cargo.CustomerService.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Features.Addresses.DeleteAddress;

public sealed class DeleteAddressCommandHandler(CustomerDbContext dbContext)
    : ICommandHandler<DeleteAddressCommand>
{
    public async Task<ErrorOr<Unit>> Handle(
        DeleteAddressCommand command,
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

        var address = await dbContext.CustomerAddresses
            .FirstOrDefaultAsync(
                a => a.Id == command.AddressId &&
                     a.CustomerId == profile.Id,
                cancellationToken);

        if (address is null)
            return Error.NotFound(
                code: "Address.NotFound",
                description: "Address not found.");

        dbContext.CustomerAddresses.Remove(address);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}