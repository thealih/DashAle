using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Application.Features.Panel;

public sealed record OfferView(Guid Id, string SellerId, string SellerName, string OfferKey,
    string Variant, long? Price, long? ListPrice, bool? Available, DateTime ObservedAt,
    bool Missing = false, string? ComparisonKey = null);

public sealed record ProductView(long Id, string BrandId, string SourceId, string Title, string Url,
    string? Image, bool Tracked, DateTime? DeletedAt, DateTime CreatedAt, DateTime? LastAttempt,
    DateTime? LastSuccess, DateTime? NextCheck, DateTime? LeaseUntil, string? Error, int FailureCount)
{
    public long? MinPrice { get; init; }
    public int? SellerCount { get; init; }
    public IReadOnlyList<OfferView>? Offers { get; init; }
    public bool? AlreadyExists { get; init; }
    public bool? BrandChanged { get; init; }
    public bool? Restored { get; init; }
    public string? BrandWarning { get; init; }
    public string? Message { get; init; }

    public static ProductView From(Product product, string brandSlug) => new(product.PublicId,
        brandSlug, product.SourceId, product.Title, ProductLinks.Canonical(product.SourceId, product.Url),
        product.ImageUrl, product.IsTracked, product.DeletedAt, product.CreatedAt, product.LastAttemptAt,
        product.LastSuccessAt, product.NextCheckAt, product.LeaseUntil, product.Error, product.FailureCount);
}
