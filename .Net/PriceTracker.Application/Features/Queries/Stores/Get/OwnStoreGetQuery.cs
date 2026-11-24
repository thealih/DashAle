using MediatR;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Queries.Stores.Get;

public sealed record OwnStoreGetQuery : IRequest<OwnStoreView>;
