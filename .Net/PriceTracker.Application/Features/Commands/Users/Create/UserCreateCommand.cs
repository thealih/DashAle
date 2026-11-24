using MediatR;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Commands.Users.Create;

public sealed record UserCreateCommand(string? Username, string? Password, string? Role)
    : IRequest<CreatedUser>;
