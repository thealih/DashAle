using PriceTracker.Domain.Models.Entities.Common;

namespace PriceTracker.Domain.Models.Entities;

public class LatestOffer : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    public required string OfferKey { get; set; }
    public string? ComparisonKey { get; set; }
    public string Variant { get; set; } = "پیش‌فرض";
    public long? Price { get; set; }
    public long? ListPrice { get; set; }
    public bool? Available { get; set; }
    public DateTime ObservedAt { get; set; }
    public bool IsMissing { get; set; }
}
