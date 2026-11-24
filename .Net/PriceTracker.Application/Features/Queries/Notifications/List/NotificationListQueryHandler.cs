using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Queries.Notifications.List;

public sealed class NotificationListQueryHandler(AppDbContext db) : IRequestHandler<NotificationListQuery, object>
{
    public async Task<object> Handle(NotificationListQuery request, CancellationToken ct)
    {
        var offset = PanelPagination.Offset(request.Page, request.PageSize);
        var query = db.Notifications.AsNoTracking();
        if (!string.IsNullOrEmpty(request.BrandId))
            query = query.Where(x => x.Brand != null && x.Brand.Slug == request.BrandId);
        if (request.ProductId is long product)
            query = query.Where(x => x.Product != null && x.Product.PublicId == product);
        if (!string.IsNullOrEmpty(request.SellerId))
            query = query.Where(x => x.Seller != null && x.Seller.SourceId == request.SellerId);
        if (!string.IsNullOrEmpty(request.Kind))
        {
            if (!Kinds.TryGetValue(request.Kind, out var kind))
                return new { items = Array.Empty<object>(), total = 0 };
            query = query.Where(x => x.Kind == kind);
        }
        if (request.Unread)
            query = query.Where(x => !x.IsRead);
        if (request.AlertsOnly)
            query = query.Where(x => x.IsAlert);
        if (request.Start is DateTime start)
            query = query.Where(x => x.CreatedAt >= start);
        if (request.End is DateTime end)
            query = query.Where(x => x.CreatedAt < end);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.PublicId)
            .Skip(offset).Take(request.PageSize).Select(x => new
            {
                id = x.PublicId, x.Kind,
                brand_id = x.Brand != null ? x.Brand.Slug : null,
                product_id = x.Product != null ? (long?)x.Product.PublicId : null,
                seller_id = x.Seller != null ? x.Seller.SourceId : null,
                x.Title, x.OldPrice, x.NewPrice, x.Percent, x.PreviousObservedAt, x.CreatedAt,
                read = x.IsRead, alerts = x.IsAlert
            }).ToListAsync(ct);

        return new
        {
            items = rows.Select(x => new
            {
                x.id, kind = Labels.GetValueOrDefault(x.Kind, "unknown"),
                x.brand_id, x.product_id, x.seller_id, x.Title, x.OldPrice, x.NewPrice, x.Percent,
                previous_observed_at = x.PreviousObservedAt, x.CreatedAt, x.read, x.alerts
            }),
            total
        };
    }

    private static readonly Dictionary<string, NotificationKind> Kinds = new(StringComparer.Ordinal)
    {
        ["price_up"] = NotificationKind.PriceUp,
        ["price_down"] = NotificationKind.PriceDown,
        ["seller_added"] = NotificationKind.SellerAdded,
        ["stock"] = NotificationKind.Stock,
        ["error"] = NotificationKind.Error,
        ["product_found"] = NotificationKind.ProductFound
    };

    private static readonly Dictionary<NotificationKind, string> Labels = Kinds.ToDictionary(x => x.Value, x => x.Key);
}
