using MediatR;

namespace PriceTracker.Application.Features.Queries.Notifications.List;

public sealed record NotificationListQuery(string? BrandId, long? ProductId, string? SellerId, string? Kind,
    bool Unread, bool AlertsOnly, DateTime? Start, DateTime? End, int Page, int PageSize) : IRequest<object>;
