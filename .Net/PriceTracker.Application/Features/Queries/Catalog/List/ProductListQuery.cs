using MediatR;

namespace PriceTracker.Application.Features.Queries.Catalog.List;

public sealed record ProductListQuery(string? BrandId, string? Search, bool? Tracked, bool Deleted,
    int Page, int PageSize) : IRequest<object>;
