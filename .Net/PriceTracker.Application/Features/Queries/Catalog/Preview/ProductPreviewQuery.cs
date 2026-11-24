using MediatR;

namespace PriceTracker.Application.Features.Queries.Catalog.Preview;

public sealed record ProductPreviewQuery(long Id) : IRequest<object>;
