using MediatR;

namespace PriceTracker.Application.Features.Queries.Reports.PositionExport;

public sealed record PricePositionExportQuery(Stream Output, string? BrandId, string? Search, string Status, int Limit)
    : IRequest;
