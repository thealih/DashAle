using PriceTracker.Domain.Models.Entities.Common;
using PriceTracker.Domain.Models.Enums;

namespace PriceTracker.Domain.Models.Entities;

public class Notification : BaseEntity
{
    public long PublicId { get; set; }
    public Guid? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid? SellerId { get; set; }
    public Seller? Seller { get; set; }
    public required NotificationKind Kind { get; set; }
    public required string Title { get; set; }
    public long? OldPrice { get; set; }
    public long? NewPrice { get; set; }
    public double? Percent { get; set; }
    public DateTime? PreviousObservedAt { get; set; }
    public bool IsRead { get; set; }
    public bool IsAlert { get; set; } = true;
}
