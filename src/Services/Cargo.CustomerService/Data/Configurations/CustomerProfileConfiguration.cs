using Cargo.CustomerService.Domain.Entities; 
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cargo.CustomerService.Data.Configurations;

public sealed class CustomerProfileConfiguration
    : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        builder.ToTable("CustomerProfiles");

        builder.HasKey(p => p.Id);

        // KeycloakUserId is the identity anchor — must be unique and indexed.
        builder.Property(p => p.KeycloakUserId)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(p => p.KeycloakUserId)
            .IsUnique();

        builder.Property(p => p.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.FullName)
            .HasMaxLength(255);

        // E.164 max length is 15 digits + '+' = 16 characters.
        builder.Property(p => p.PhoneNumber)
            .HasMaxLength(16);

        // Store enum as string in the database — readable in SQL queries
        // and safe against reordering.
        builder.Property(p => p.OnboardingStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        // One-to-many: one profile has many documents.
        // Cascade delete: deleting a profile deletes all its documents.
        builder.HasMany(p => p.Documents)
            .WithOne(d => d.Customer)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-many: one profile has many addresses.
        builder.HasMany(p => p.Addresses)
            .WithOne(a => a.Customer)
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}