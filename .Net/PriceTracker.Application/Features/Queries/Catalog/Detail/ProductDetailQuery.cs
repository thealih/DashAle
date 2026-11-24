using MediatR;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Queries.Catalog.Detail;

public sealed record ProductDetailQuery(long Id) : IRequest<ProductView>;
