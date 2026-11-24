using MediatR;

namespace PriceTracker.Application.Features.Commands.Catalog.QueueCheck;

public sealed record ProductQueueCheckCommand(long Id, string? Actor) : IRequest;
