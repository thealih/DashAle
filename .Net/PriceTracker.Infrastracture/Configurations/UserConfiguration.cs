using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Infrastracture.Configurations
{
    public class UserConfiguration : EntityTypeConfiguration<User>
    {
        protected override void AppendConfig(EntityTypeBuilder<User> builder)
        {
            builder.Property(x => x.UserName).HasColumnType("varchar").HasMaxLength(100).IsRequired();
        }
    }
}
