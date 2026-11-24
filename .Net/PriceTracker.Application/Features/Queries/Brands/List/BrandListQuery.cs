using MediatR;

namespace PriceTracker.Application.Features.Queries.Brands.List;

public sealed record BrandListQuery : IRequest<object>;
