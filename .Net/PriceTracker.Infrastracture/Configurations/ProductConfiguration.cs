using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Infrastracture.Configurations;

public class ProductConfiguration : EntityTypeConfiguration<Product>
{
    protected override void AppendConfig(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", table => table.HasCheckConstraint(
            "CK_Products_ArchiveState",
            "([IsDeleted] = 0 AND [DeletedAt] IS NULL) OR " +
            "([IsDeleted] = 1 AND [DeletedAt] IS NOT NULL AND [IsTracked] = 0 " +
            "AND [NextCheckAt] IS NULL AND [LeaseUntil] IS NULL)"));

        builder.Property(x => x.PublicId).UseIdentityColumn();
        builder.Property(x => x.SourceId).HasMaxLength(80);
        builder.Property(x => x.Title).HasMaxLength(600);

        builder.HasIndex(x => x.PublicId).IsUnique();
        builder.HasIndex(x => x.SourceId).IsUnique();
        builder.HasIndex(x => new { x.BrandId, x.IsDeleted, x.IsTracked });
        builder.HasIndex(x => new { x.IsDeleted, x.IsTracked, x.NextCheckAt });

        builder.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasQueryFilter(x => !x.IsDeleted && !x.Brand.IsDeleted);
    }
}
