namespace PriceTracker.Domain.Models.Entities;

public sealed class UserRefreshToken
{
    public required string Hash { get; init; }
    public Guid SessionId { get; init; }
    public UserSession Session { get; init; } = null!;
    public DateTime? UsedAt { get; set; }
}
