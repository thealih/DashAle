using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Commands.Catalog.QueueCheck;

public sealed class ProductQueueCheckCommandHandler(AppDbContext db) : IRequestHandler<ProductQueueCheckCommand>
{
    public async Task Handle(ProductQueueCheckCommand request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var query = db.Products.Where(x => x.PublicId == request.Id);
        if (await query.Where(x => x.IsTracked).ExecuteUpdateAsync(set => set.SetProperty(x => x.NextCheckAt, now)
            .SetProperty(x => x.ModifiedAt, now).SetProperty(x => x.ModifiedBy, request.Actor), ct) != 0)
            return;
        if (!await query.AnyAsync(ct))
            throw PanelException.MissingProduct();
        throw new PanelException(422, "ابتدا پایش این محصول را فعال کنید.");
    }
}
