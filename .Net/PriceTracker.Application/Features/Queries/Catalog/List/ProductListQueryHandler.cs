using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Queries.Catalog.List;

public sealed class ProductListQueryHandler(AppDbContext db) : IRequestHandler<ProductListQuery, object>
{
    public async Task<object> Handle(ProductListQuery request, CancellationToken ct)
    {
        var offset = PanelPagination.Offset(request.Page, request.PageSize);
        IQueryable<Product> query = db.Products.AsNoTracking();
        if (request.Deleted)
            query = query.IgnoreQueryFilters().Where(x => x.IsDeleted && !x.Brand.IsDeleted);
        if (!string.IsNullOrEmpty(request.BrandId))
            query = query.Where(x => x.Brand.Slug == request.BrandId);
        if (request.Tracked is bool tracked)
            query = query.Where(x => x.IsTracked == tracked);
        if (!string.IsNullOrEmpty(request.Search))
            query = query.Where(x => x.Title.Contains(request.Search));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.IsTracked).ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.PublicId).Skip(offset).Take(request.PageSize).Select(p => new
            {
                Product = p, Slug = p.Brand.Slug,
                Price = db.LatestOffers.IgnoreQueryFilters().Where(x => x.ProductId == p.Id && !x.IsDeleted
                    && !x.Seller.IsDeleted && !x.IsMissing && x.Available != false).Min(x => x.Price),
                Sellers = db.LatestOffers.IgnoreQueryFilters().Where(x => x.ProductId == p.Id && !x.IsDeleted
                    && !x.Seller.IsDeleted && !x.IsMissing).Select(x => x.SellerId).Distinct().Count()
            }).ToListAsync(ct);
        return new
        {
            items = rows.Select(x => ProductView.From(x.Product, x.Slug) with
            {
                MinPrice = x.Price,
                SellerCount = x.Product.LastSuccessAt != null || x.Sellers > 0 ? x.Sellers : null
            }),
            total, page = request.Page, page_size = request.PageSize
        };
    }
}
