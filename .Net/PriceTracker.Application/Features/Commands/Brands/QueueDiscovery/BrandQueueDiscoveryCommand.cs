using MediatR;

namespace PriceTracker.Application.Features.Commands.Brands.QueueDiscovery;

public sealed record BrandQueueDiscoveryCommand(string BrandId) : IRequest<Guid>;
