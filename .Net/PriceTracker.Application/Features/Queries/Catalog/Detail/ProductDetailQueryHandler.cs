using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Queries.Catalog.Detail;

public sealed class ProductDetailQueryHandler(AppDbContext db) : IRequestHandler<ProductDetailQuery, ProductView>
{
    public async Task<ProductView> Handle(ProductDetailQuery request, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().Include(x => x.Brand)
            .SingleOrDefaultAsync(x => x.PublicId == request.Id, ct) ?? throw PanelException.MissingProduct();
        var offers = await db.LatestOffers.Where(x => x.ProductId == product.Id)
            .OrderBy(x => x.IsMissing).ThenBy(x => x.Price).ThenBy(x => x.Id)
            .Select(x => new OfferView(x.Id, x.Seller.SourceId, x.Seller.Name, x.OfferKey, x.Variant,
                x.Price, x.ListPrice, x.Available, x.ObservedAt, x.IsMissing, x.ComparisonKey))
            .ToListAsync(ct);
        return ProductView.From(product, product.Brand.Slug) with { Offers = offers };
    }
}
