using MediatR;

namespace PriceTracker.Application.Features.Queries.Catalog.History;

public sealed record ProductHistoryQuery(long Id, string? SellerId, DateTime? Start, DateTime? End,
    int Page, int PageSize) : IRequest<object>;
