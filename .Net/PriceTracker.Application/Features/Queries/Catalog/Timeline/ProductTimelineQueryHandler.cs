using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Queries.Catalog.Timeline;

public sealed class ProductTimelineQueryHandler(AppDbContext db) : IRequestHandler<ProductTimelineQuery, object>
{
    public async Task<object> Handle(ProductTimelineQuery request, CancellationToken ct)
    {
        var query = await ProductHistoryScope.LoadAsync(db, request.Id, request.SellerId, ct);
        var points = await query.Where(x => x.Price != null && x.Available != false)
            .GroupBy(x => x.ObservedAt).Select(x => new { time = x.Key, price = x.Min(p => p.Price) })
            .OrderByDescending(x => x.time).Take(168).ToListAsync(ct);
        points.Reverse();
        return points;
    }
}
