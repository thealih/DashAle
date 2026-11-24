using MediatR;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Commands.Stores.Update;

public sealed record OwnStoreUpdateCommand(string SellerId, string SellerName, int UndercutToman) : IRequest<OwnStoreView>;
