using Cargo.BuildingBlocks.Messaging.Outbox;
using Cargo.CustomerService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cargo.CustomerService.Data;

public sealed class CustomerDbContext(DbContextOptions<CustomerDbContext> options)
    : DbContext(options), IOutboxDbContext
{
    public DbSet<CustomerProfile>  CustomerProfiles  => Set<CustomerProfile>();
    public DbSet<CustomerAddress>  CustomerAddresses => Set<CustomerAddress>();
    public DbSet<CustomerDocument> CustomerDocuments => Set<CustomerDocument>();

    // ── Outbox (IOutboxDbContext) ─────────────────────────────────────────
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Discovers and applies all IEntityTypeConfiguration<T> classes
        // in this assembly. This is the only line needed here.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}