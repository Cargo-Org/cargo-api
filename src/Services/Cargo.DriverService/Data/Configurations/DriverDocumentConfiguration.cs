using Cargo.DriverService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cargo.DriverService.Data.Configurations;

public sealed class DriverDocumentConfiguration
    : IEntityTypeConfiguration<DriverDocument>
{
    public void Configure(EntityTypeBuilder<DriverDocument> builder)
    {
        builder.ToTable("DriverDocuments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        // Object keys follow format: drivers/{guid}/{type}-{guid}.ext
        // Maximum practical length is well under 500.
        builder.Property(d => d.ObjectKey)
            .IsRequired()
            .HasMaxLength(500);

        // Index on ObjectKey — used to verify ownership at document registration.
        builder.HasIndex(d => d.ObjectKey)
            .IsUnique();

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.ReviewStatus)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.ReviewNote)
            .HasMaxLength(1000);

        builder.Property(d => d.ReviewedByKeycloakId)
            .HasMaxLength(255);

        builder.Property(d => d.UploadedAt)
            .IsRequired();

        // Index on DriverId — all document queries are by driver.
        builder.HasIndex(d => d.DriverId);
    }
}
