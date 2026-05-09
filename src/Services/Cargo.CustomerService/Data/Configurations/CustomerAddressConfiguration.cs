using Cargo.CustomerService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cargo.CustomerService.Data.Configurations;

public sealed class CustomerAddressConfiguration
    : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("CustomerAddresses");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Label)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.AddressLine)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.City)
            .IsRequired()
            .HasMaxLength(100);

        // ISO 3166-1 alpha-2 is always exactly 2 characters.
        builder.Property(a => a.Country)
            .IsRequired()
            .HasMaxLength(2);

        builder.Property(a => a.PostalCode)
            .HasMaxLength(20);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        // Index on CustomerId — all address queries are by customer.
        builder.HasIndex(a => a.CustomerId);
    }
}