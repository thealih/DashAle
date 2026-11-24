using PriceTracker.Domain.Models.Entities.Common;

namespace PriceTracker.Domain.Models.Entities;

public class Seller : BaseEntity
{
    public required string SourceId { get; set; }
    public required string Name { get; set; }
}
