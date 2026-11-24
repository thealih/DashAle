namespace PriceTracker.Domain.IRepositories.Common;

public interface IUnitOfWork
{
    IBrandRepository BrandRep { get; }
    IProductRepository ProductRep { get; }
    ISellerRepository SellerRep { get; }
    ILatestOfferRepository LatestOfferRep { get; }
    IPriceObservationRepository PriceObservationRep { get; }
    INotificationRepository NotificationRep { get; }
    IAppSettingRepository AppSettingRep { get; }
    IDiscoveryTaskRepository DiscoveryTaskRep { get; }
    IUserRepository UserRep { get; }
    IUserSessionRepository UserSessionRep { get; }
    IUserRefreshTokenRepository UserRefreshTokenRep { get; }

    Task<int> SaveAsync(CancellationToken cancellationToken = default);
}
