using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Infrastracture.Configurations;

public class SellerConfiguration : EntityTypeConfiguration<Seller>
{
    protected override void AppendConfig(EntityTypeBuilder<Seller> builder)
    {
        builder.ToTable("Sellers");
        builder.Property(x => x.SourceId).HasMaxLength(100);
        builder.Property(x => x.Name).HasMaxLength(300);
        builder.HasIndex(x => x.SourceId).IsUnique();
    }
}
