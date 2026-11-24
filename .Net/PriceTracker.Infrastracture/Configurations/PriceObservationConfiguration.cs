using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Infrastracture.Configurations;

public class PriceObservationConfiguration : EntityTypeConfiguration<PriceObservation>
{
    protected override void AppendConfig(EntityTypeBuilder<PriceObservation> builder)
    {
        builder.ToTable("PriceObservations");
        builder.Property(x => x.OfferKey).HasMaxLength(200);
        builder.Property(x => x.Variant).HasMaxLength(500);
        builder.HasIndex(x => x.ObservedAt);
        builder.HasIndex(x => new { x.ProductId, x.ObservedAt });
        builder.HasIndex(x => new { x.ProductId, x.SellerId, x.ObservedAt });

        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Seller).WithMany().HasForeignKey(x => x.SellerId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasQueryFilter(x => !x.IsDeleted && !x.Product.IsDeleted
            && !x.Product.Brand.IsDeleted && !x.Seller.IsDeleted);
    }
}
