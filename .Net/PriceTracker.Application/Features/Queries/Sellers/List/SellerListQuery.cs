using MediatR;

namespace PriceTracker.Application.Features.Queries.Sellers.List;

public sealed record SellerListQuery : IRequest<object>;
