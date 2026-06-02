using Microsoft.EntityFrameworkCore;

namespace Cargo.BuildingBlocks.Messaging.Outbox;

/// <summary>
/// Each service's DbContext implements this interface so the shared outbox
/// infrastructure can access OutboxMessages without knowing the concrete
/// context type.
/// </summary>
public interface IOutboxDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
