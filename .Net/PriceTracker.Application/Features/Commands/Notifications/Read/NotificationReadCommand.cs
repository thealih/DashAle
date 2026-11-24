using MediatR;

namespace PriceTracker.Application.Features.Commands.Notifications.Read;

public sealed record NotificationReadCommand(long[]? Ids, string? Actor) : IRequest;
