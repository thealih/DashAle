using PriceTracker.Domain.Models.Entities.Common;

namespace PriceTracker.Domain.Models.Entities;

public class Brand : BaseEntity
{
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public required string EnglishName { get; set; }
    public required string Aliases { get; set; }
    public DateTime? LastDiscoveredAt { get; set; }
    public string? DiscoveryError { get; set; }
}
