using Microsoft.EntityFrameworkCore;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Panel;

internal static class ProductHistoryScope
{
    internal static async Task<IQueryable<PriceObservation>> LoadAsync(AppDbContext db, long publicId, string? sellerId, CancellationToken ct)
    {
        var productId = await db.Products.Where(x => x.PublicId == publicId).Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(ct) ?? throw PanelException.MissingProduct();
        var query = db.PriceObservations.AsNoTracking().Where(x => x.ProductId == productId);
        return string.IsNullOrEmpty(sellerId) ? query : query.Where(x => x.Seller.SourceId == sellerId);
    }
}
