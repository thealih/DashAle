using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Panel;

public sealed record NoticeList(string? BrandId, long? ProductId, string? SellerId, string? Kind,
    bool Unread, bool AlertsOnly, DateTime? Start, DateTime? End, int Page, int PageSize) : IRequest<object>;

public sealed record ReadNotices(long[]? Ids, string? Actor) : IRequest;

public sealed class NoticeQueries(AppDbContext db) : IRequestHandler<NoticeList, object>, IRequestHandler<ReadNotices>
{
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

    public async Task<object> Handle(NoticeList request, CancellationToken ct)
    {
        var offset = PanelPagination.Offset(request.Page, request.PageSize, 100);
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

    public async Task Handle(ReadNotices request, CancellationToken ct)
    {
        if (request.Ids is { Length: > 1000 })
            throw new PanelException(422, "حداکثر ۱۰۰۰ اعلان را هم‌زمان انتخاب کنید.");

        var query = db.Notifications.Where(x => !x.IsRead);
        if (request.Ids is { } ids)
        {
            if (ids.Length == 0)
                return;
            query = query.Where(x => ids.Contains(x.PublicId));
        }
        var now = DateTime.UtcNow;
        await query.ExecuteUpdateAsync(set => set.SetProperty(x => x.IsRead, true)
            .SetProperty(x => x.ModifiedAt, now).SetProperty(x => x.ModifiedBy, request.Actor), ct);
    }
}
