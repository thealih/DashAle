using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Infrastracture.Configurations;

public class NotificationConfiguration : EntityTypeConfiguration<Notification>
{
    protected override void AppendConfig(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.Property(x => x.PublicId).UseIdentityColumn();
        builder.HasIndex(x => x.PublicId).IsUnique();
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.Kind, x.CreatedAt });
        builder.HasIndex(x => new { x.IsRead, x.CreatedAt });

        builder.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Seller).WithMany().HasForeignKey(x => x.SellerId)
            .OnDelete(DeleteBehavior.NoAction);

        // Check the stored FK so a hidden product cannot turn its notification into a system message.
        builder.HasQueryFilter(x => !x.IsDeleted && (x.ProductId == null
            || (x.Product != null && !x.Product.IsDeleted && !x.Product.Brand.IsDeleted)));
    }
}
