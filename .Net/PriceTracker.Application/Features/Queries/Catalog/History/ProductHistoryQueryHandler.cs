using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Queries.Catalog.History;

public sealed class ProductHistoryQueryHandler(AppDbContext db) : IRequestHandler<ProductHistoryQuery, object>
{
    public async Task<object> Handle(ProductHistoryQuery request, CancellationToken ct)
    {
        var offset = PanelPagination.Offset(request.Page, request.PageSize);
        var query = await ProductHistoryScope.LoadAsync(db, request.Id, request.SellerId, ct);
        if (request.Start is DateTime start)
            query = query.Where(x => x.ObservedAt >= start);
        if (request.End is DateTime end)
            query = query.Where(x => x.ObservedAt < end);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.ObservedAt).ThenByDescending(x => x.Id)
            .Skip(offset).Take(request.PageSize)
            .Select(x => new OfferView(x.Id, x.Seller.SourceId, x.Seller.Name, x.OfferKey, x.Variant,
                x.Price, x.ListPrice, x.Available, x.ObservedAt, false, null)).ToListAsync(ct);
        return new { items, total, page = request.Page };
    }
}
