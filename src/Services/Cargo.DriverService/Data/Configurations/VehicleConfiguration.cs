using Cargo.DriverService.Domain.Entities;
using Cargo.DriverService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cargo.DriverService.Data.Configurations;

public sealed class VehicleConfiguration
    : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.VehicleNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(v => v.VehicleNumber)
            .IsUnique();

        builder.Property(v => v.VehicleModel)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(v => v.VehicleType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.VehicleColor)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.ManufactureYear)
            .IsRequired();

        builder.Property(v => v.LicensePlate)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(v => v.CreatedAt)
            .IsRequired();

        // ── License file metadata ──────────────────────────────────────────
        builder.Property(v => v.LicenseObjectKey)
            .HasMaxLength(500);

        builder.Property(v => v.LicenseContentType)
            .HasMaxLength(100);

        builder.Property(v => v.LicenseOriginalFileName)
            .HasMaxLength(255);

        // Store enum as string — readable in SQL and safe against reordering.
        builder.Property(v => v.LicenseReviewStatus)
            .HasConversion<string?>()
            .HasMaxLength(50);

        builder.Property(v => v.LicenseReviewNote)
            .HasMaxLength(1000);

        builder.Property(v => v.LicenseReviewedByKeycloakId)
            .HasMaxLength(255);

        // ── Relationships ──────────────────────────────────────────────────
        // Many vehicles belong to one driver. Cascade: deleting a driver
        // deletes all their vehicles.
        builder.HasOne(v => v.Driver)
            .WithMany(p => p.Vehicles)
            .HasForeignKey(v => v.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on DriverId — all vehicle queries are by driver.
        builder.HasIndex(v => v.DriverId);
    }
}
