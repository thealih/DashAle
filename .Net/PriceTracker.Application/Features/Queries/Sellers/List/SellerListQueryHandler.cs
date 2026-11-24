using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Queries.Sellers.List;

public sealed class SellerListQueryHandler(AppDbContext db) : IRequestHandler<SellerListQuery, object>
{
    public async Task<object> Handle(SellerListQuery request, CancellationToken ct) =>
        await db.Sellers.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.SourceId)
            .Select(x => new { id = x.SourceId, x.Name }).ToListAsync(ct);
}
