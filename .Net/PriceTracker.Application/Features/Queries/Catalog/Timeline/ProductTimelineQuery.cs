using MediatR;

namespace PriceTracker.Application.Features.Queries.Catalog.Timeline;

public sealed record ProductTimelineQuery(long Id, string? SellerId) : IRequest<object>;
