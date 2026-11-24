using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Infrastracture.Configurations;

public class UserConfiguration : EntityTypeConfiguration<User>
{
    protected override void AppendConfig(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.Property(x => x.UserName)
            .HasMaxLength(100)
            .UseCollation("Latin1_General_100_CI_AS")
            .IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        builder.HasIndex(x => x.UserName).IsUnique();
    }
}
