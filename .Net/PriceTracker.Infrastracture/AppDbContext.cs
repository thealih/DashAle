using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PriceTracker.Infrastracture.Configurations;
using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Infrastracture
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Brand> Brands => Set<Brand>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Seller> Sellers => Set<Seller>();
        public DbSet<LatestOffer> LatestOffers => Set<LatestOffer>();
        public DbSet<PriceObservation> PriceObservations => Set<PriceObservation>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<AppSetting> AppSettings => Set<AppSetting>();
        public DbSet<DiscoveryTask> DiscoveryTasks => Set<DiscoveryTask>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserSession> UserSessions => Set<UserSession>();
        public DbSet<UserRefreshToken> UserRefreshTokens => Set<UserRefreshToken>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }

    public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter() : base(
            value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
        {
        }
    }
}
