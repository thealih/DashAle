using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Infrastracture.Configurations;

public sealed class UserRefreshTokenConfiguration : IEntityTypeConfiguration<UserRefreshToken>
{
    public void Configure(EntityTypeBuilder<UserRefreshToken> builder)
    {
        builder.ToTable("UserRefreshTokens");
        builder.HasKey(x => x.Hash);
        builder.Property(x => x.Hash).HasMaxLength(64).IsUnicode(false).IsFixedLength().ValueGeneratedNever();
        builder.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(x => !x.Session.User.IsDeleted);
    }
}
