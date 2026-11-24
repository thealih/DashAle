using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Commands.Catalog.Update;

public sealed class ProductUpdateCommandHandler(AppDbContext db) : IRequestHandler<ProductUpdateCommand, int>
{
    public async Task<int> Handle(ProductUpdateCommand request, CancellationToken ct)
    {
        var ids = request.Ids.Distinct().ToArray();
        var timestamp = DateTime.UtcNow;
        var query = db.Products.Where(x => ids.Contains(x.PublicId));
        switch (request.Action)
        {
            case ProductAction.Delete:
                return await query.ExecuteUpdateAsync(set => set
                    .SetProperty(x => x.IsDeleted, true).SetProperty(x => x.DeletedAt, timestamp)
                    .SetProperty(x => x.IsTracked, false).SetProperty(x => x.NextCheckAt, (DateTime?)null)
                    .SetProperty(x => x.LeaseUntil, (DateTime?)null)
                    .SetProperty(x => x.ModifiedAt, timestamp).SetProperty(x => x.ModifiedBy, request.Actor), ct);
            case ProductAction.Restore:
                return await query.IgnoreQueryFilters().Where(x => x.IsDeleted && !x.Brand.IsDeleted)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsDeleted, false)
                    .SetProperty(x => x.DeletedAt, (DateTime?)null)
                    .SetProperty(x => x.ModifiedAt, timestamp).SetProperty(x => x.ModifiedBy, request.Actor), ct);
            default:
                var track = request.Action == ProductAction.Track;
                return await query.ExecuteUpdateAsync(set => set.SetProperty(x => x.IsTracked, track)
                    .SetProperty(x => x.NextCheckAt, track ? timestamp : (DateTime?)null)
                    .SetProperty(x => x.LeaseUntil, x => track ? x.LeaseUntil : null)
                    .SetProperty(x => x.ModifiedAt, timestamp).SetProperty(x => x.ModifiedBy, request.Actor), ct);
        }
    }
}
