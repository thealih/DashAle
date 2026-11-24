using MediatR;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Queries.Stores.Get;

public sealed class OwnStoreGetQueryHandler(AppDbContext db) : IRequestHandler<OwnStoreGetQuery, OwnStoreView>
{
    public Task<OwnStoreView> Handle(OwnStoreGetQuery request, CancellationToken ct) => OwnStore.LoadAsync(db, ct);
}
