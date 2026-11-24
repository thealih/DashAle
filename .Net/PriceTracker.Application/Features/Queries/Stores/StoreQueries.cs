using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Queries.Stores;

public sealed record GetOwnStore : IRequest<OwnStoreView>;
public sealed record SellerList : IRequest<object>;

public sealed class StoreQueries(AppDbContext db) : IRequestHandler<GetOwnStore, OwnStoreView>, IRequestHandler<SellerList, object>
{
    public Task<OwnStoreView> Handle(GetOwnStore request, CancellationToken ct) => OwnStore.LoadAsync(db, ct);

    public async Task<object> Handle(SellerList request, CancellationToken ct) =>
        await db.Sellers.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.SourceId)
            .Select(x => new { id = x.SourceId, x.Name }).ToListAsync(ct);
}
