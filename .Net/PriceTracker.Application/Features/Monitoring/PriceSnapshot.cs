using Microsoft.EntityFrameworkCore;
using PriceTracker.Domain.IServices;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Monitoring;

internal static class PriceSnapshot
{
    // Callers own the transaction and save the complete snapshot together with the product status.
    public static async Task ApplyAsync(AppDbContext db, Product product, IReadOnlyList<SourceOffer> offers,
        DateTime observedAt, double threshold, CancellationToken ct)
    {
        var current = await db.LatestOffers.Where(x => x.ProductId == product.Id)
            .ToDictionaryAsync(x => x.OfferKey, ct);
        var knownSellers = current.Values.Select(x => x.SellerId).ToHashSet();
        var ids = offers.Select(x => x.SellerId).Distinct().ToArray();
        var sellers = await db.Sellers.Where(x => ids.Contains(x.SourceId)).ToDictionaryAsync(x => x.SourceId, ct);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var offer in offers)
        {
            if (!seen.Add(offer.OfferKey))
                throw new ProductSourceException("پیشنهادهای فروش تکراری دریافت شد؛ قیمت‌ها ثبت نشدند.");
            if (!sellers.TryGetValue(offer.SellerId, out var seller))
            {
                seller = new Seller { SourceId = offer.SellerId, Name = offer.SellerName };
                db.Sellers.Add(seller);
                sellers.Add(offer.SellerId, seller);
            }
            seller.Name = offer.SellerName;

            if (!current.TryGetValue(offer.OfferKey, out var latest))
            {
                latest = new LatestOffer { Product = product, Seller = seller, OfferKey = offer.OfferKey };
                db.LatestOffers.Add(latest);
                if (product.LastSuccessAt != null && knownSellers.Add(seller.Id))
                    db.Notifications.Add(Notice(NotificationKind.SellerAdded,
                        $"فروشندهٔ «{seller.Name}» به {product.Title} اضافه شد.", product, seller, observedAt));
            }
            else
            {
                if (latest.Price is > 0 and long oldPrice && offer.Price is long newPrice && newPrice != oldPrice)
                {
                    var percent = ((double)newPrice - oldPrice) / oldPrice * 100;
                    var notice = Notice(newPrice > oldPrice ? NotificationKind.PriceUp : NotificationKind.PriceDown,
                        $"تغییر قیمت «{product.Title}» توسط {seller.Name} · {offer.Variant}", product, seller, observedAt);
                    notice.OldPrice = oldPrice;
                    notice.NewPrice = newPrice;
                    notice.Percent = percent;
                    notice.PreviousObservedAt = latest.ObservedAt;
                    notice.IsAlert = Math.Abs(percent) >= threshold;
                    db.Notifications.Add(notice);
                }
                if (latest.Available is bool before && offer.Available is bool after && before != after)
                    db.Notifications.Add(Notice(NotificationKind.Stock,
                        $"{product.Title} نزد {seller.Name} {(after ? "موجود" : "ناموجود")} شد · {offer.Variant}",
                        product, seller, observedAt));
            }

            latest.Variant = offer.Variant;
            latest.ComparisonKey = offer.ComparisonKey;
            latest.Price = offer.Price;
            latest.ListPrice = offer.ListPrice;
            latest.Available = offer.Available;
            latest.ObservedAt = observedAt;
            latest.IsMissing = false;
            db.PriceObservations.Add(new PriceObservation
            {
                Product = product, Seller = seller, OfferKey = offer.OfferKey, Variant = offer.Variant,
                Price = offer.Price, ListPrice = offer.ListPrice, Available = offer.Available, ObservedAt = observedAt
            });
        }

        foreach (var (key, latest) in current)
            if (!seen.Contains(key))
                latest.IsMissing = true;
    }

    private static Notification Notice(NotificationKind kind, string title, Product product, Seller seller, DateTime now) => new()
    {
        Kind = kind, Title = title, Product = product, Seller = seller, BrandId = product.BrandId, CreatedAt = now
    };
}
