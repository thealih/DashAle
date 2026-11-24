namespace PriceTracker.Domain.Models.Entities;

public sealed class UserSession
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public User User { get; init; } = null!;
    public DateTime ExpiresAt { get; init; }
}
