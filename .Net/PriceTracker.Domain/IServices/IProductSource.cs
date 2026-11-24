namespace PriceTracker.Domain.IServices;

public interface IProductSource
{
    Task<SourceProduct> GetProductAsync(string sourceId, CancellationToken cancellationToken);
}

public sealed record SourceProduct(string Title, string? ImageUrl, string BrandTitle,
    string Availability, IReadOnlyList<SourceOffer> Offers);

public sealed record SourceOffer(string SellerId, string SellerName, string OfferKey, string ComparisonKey,
    string Variant, long? Price, long? ListPrice, bool? Available);

public sealed class ProductSourceException(string message) : Exception(message);
