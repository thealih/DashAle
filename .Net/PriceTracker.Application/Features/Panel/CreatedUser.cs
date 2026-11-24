namespace PriceTracker.Application.Features.Panel;

public sealed record CreatedUser(Guid Id, string Username, string Role, bool IsActive);
