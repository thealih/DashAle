using MediatR;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Commands.Users.ChangeRole;

public sealed record UserChangeRoleCommand(Guid TargetId, string? Role, Guid ActorId, Guid ActorSessionId)
    : IRequest<CreatedUser>;
