using Cargo.BuildingBlocks.Messaging.Outbox;
using Cargo.DriverService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cargo.DriverService.Data;

public sealed class DriverDbContext(DbContextOptions<DriverDbContext> options)
    : DbContext(options), IOutboxDbContext
{
    public DbSet<DriverProfile>  DriverProfiles  => Set<DriverProfile>();
    public DbSet<DriverDocument> DriverDocuments => Set<DriverDocument>();
    public DbSet<Vehicle>        Vehicles        => Set<Vehicle>();

    // ── Outbox (IOutboxDbContext) ─────────────────────────────────────────
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Discovers and applies all IEntityTypeConfiguration<T> classes
        // in this assembly. This is the only line needed here.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DriverDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
