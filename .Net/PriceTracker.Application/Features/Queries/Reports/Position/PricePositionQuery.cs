using MediatR;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Queries.Reports.Position;

public sealed record PricePositionQuery(string? BrandId, string? Search, string Status, int Page, int PageSize)
    : IRequest<PositionResult>;
