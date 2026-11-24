using MediatR;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Queries.Reports.Position;

public sealed class PricePositionQueryHandler(PricePosition comparison) : IRequestHandler<PricePositionQuery, PositionResult>
{
    public Task<PositionResult> Handle(PricePositionQuery request, CancellationToken ct) =>
        comparison.CompareAsync(request.BrandId, request.Search, request.Status, request.Page, request.PageSize, ct);
}
