using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Infrastracture.Configurations;

public class BrandConfiguration : EntityTypeConfiguration<Brand>
{
    protected override void AppendConfig(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");
        builder.Property(x => x.Slug).HasMaxLength(40);
        builder.Property(x => x.Name).HasMaxLength(80);
        builder.Property(x => x.EnglishName).HasMaxLength(80);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasData(InitialReferenceData.Brands);
    }
}
