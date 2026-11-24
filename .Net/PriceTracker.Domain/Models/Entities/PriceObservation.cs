using PriceTracker.Domain.Models.Entities.Common;

namespace PriceTracker.Domain.Models.Entities;

public class PriceObservation : BaseEntity
{
    public Guid ProductId { get; init; }
    public Product Product { get; init; } = null!;
    public Guid SellerId { get; init; }
    public Seller Seller { get; init; } = null!;
    public required string OfferKey { get; init; }
    public required string Variant { get; init; }
    public long? Price { get; init; }
    public long? ListPrice { get; init; }
    public bool? Available { get; init; }
    public DateTime ObservedAt { get; init; }
}
