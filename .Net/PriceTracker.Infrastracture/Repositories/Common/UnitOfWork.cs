using PriceTracker.Domain.IRepositories;
using PriceTracker.Domain.IRepositories.Common;

namespace PriceTracker.Infrastracture.Repositories.Common;

public sealed class UnitOfWork(
    AppDbContext context,
    IBrandRepository brandRep,
    IProductRepository productRep,
    ISellerRepository sellerRep,
    ILatestOfferRepository latestOfferRep,
    IPriceObservationRepository priceObservationRep,
    INotificationRepository notificationRep,
    IAppSettingRepository appSettingRep,
    IDiscoveryTaskRepository discoveryTaskRep,
    IUserRepository userRep,
    IUserSessionRepository userSessionRep,
    IUserRefreshTokenRepository userRefreshTokenRep) : IUnitOfWork
{
    public IBrandRepository BrandRep { get; } = brandRep;
    public IProductRepository ProductRep { get; } = productRep;
    public ISellerRepository SellerRep { get; } = sellerRep;
    public ILatestOfferRepository LatestOfferRep { get; } = latestOfferRep;
    public IPriceObservationRepository PriceObservationRep { get; } = priceObservationRep;
    public INotificationRepository NotificationRep { get; } = notificationRep;
    public IAppSettingRepository AppSettingRep { get; } = appSettingRep;
    public IDiscoveryTaskRepository DiscoveryTaskRep { get; } = discoveryTaskRep;
    public IUserRepository UserRep { get; } = userRep;
    public IUserSessionRepository UserSessionRep { get; } = userSessionRep;
    public IUserRefreshTokenRepository UserRefreshTokenRep { get; } = userRefreshTokenRep;

    public Task<int> SaveAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
