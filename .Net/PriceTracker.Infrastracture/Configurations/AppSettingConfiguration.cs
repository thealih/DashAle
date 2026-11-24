using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Infrastracture.Configurations;

public class AppSettingConfiguration : EntityTypeConfiguration<AppSetting>
{
    protected override void AppendConfig(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("AppSettings");
        builder.Property(x => x.Key).HasMaxLength(80);
        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasData(InitialReferenceData.AppSettings);
    }
}
