using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Infrastracture.Configurations;

public class DiscoveryTaskConfiguration : EntityTypeConfiguration<DiscoveryTask>
{
    protected override void AppendConfig(EntityTypeBuilder<DiscoveryTask> builder)
    {
        builder.ToTable("DiscoveryTasks");
        builder.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.BrandId, x.Status });
        builder.HasIndex(x => new { x.Status, x.LeaseUntil, x.CreatedAt });
    }
}
