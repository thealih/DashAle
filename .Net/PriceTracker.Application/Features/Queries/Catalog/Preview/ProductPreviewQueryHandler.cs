using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.IServices;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Queries.Catalog.Preview;

public sealed class ProductPreviewQueryHandler(AppDbContext db, IProductSource source) : IRequestHandler<ProductPreviewQuery, object>
{
    public async Task<object> Handle(ProductPreviewQuery request, CancellationToken ct)
    {
        var sourceId = await db.Products.Where(x => x.PublicId == request.Id).Select(x => x.SourceId)
            .SingleOrDefaultAsync(ct) ?? throw PanelException.MissingProduct();
        var snapshot = await ProductSourceReader.ReadAsync(source, sourceId, ct);
        var now = DateTime.UtcNow;
        return new
        {
            product_id = request.Id, observed_at = now, mode = "live", availability = snapshot.Availability,
            seller_count = snapshot.Offers.Select(x => x.SellerId).Distinct().Count(),
            offers = snapshot.Offers.OrderBy(x => x.Price == null).ThenBy(x => x.Price).Select((x, i) => new
            {
                id = -(i + 1), x.SellerId, x.SellerName, x.OfferKey, x.Variant, x.Price, x.ListPrice,
                x.Available, observed_at = now, missing = false
            })
        };
    }
}
