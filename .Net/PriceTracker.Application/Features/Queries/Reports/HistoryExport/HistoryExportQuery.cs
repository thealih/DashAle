using MediatR;

namespace PriceTracker.Application.Features.Queries.Reports.HistoryExport;

public sealed record HistoryExportQuery(Stream Output, string? BrandId, long? ProductId, string? SellerId,
    DateTime? Start, DateTime? End, bool IncludeHistory, int Limit) : IRequest;
