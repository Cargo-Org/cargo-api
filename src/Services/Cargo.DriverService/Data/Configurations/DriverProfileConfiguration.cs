using Cargo.DriverService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cargo.DriverService.Data.Configurations;

public sealed class DriverProfileConfiguration
    : IEntityTypeConfiguration<DriverProfile>
{
    public void Configure(EntityTypeBuilder<DriverProfile> builder)
    {
        builder.ToTable("DriverProfiles");

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

        builder.Property(p => p.FirstName)
            .HasMaxLength(128);
        builder.Property(p => p.LastName)
            .HasMaxLength(128);

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
            .WithOne(d => d.Driver)
            .HasForeignKey(d => d.DriverId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
