using System.Data;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Domain.IServices;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Panel;

public enum ProductAction { Track, Untrack, Delete, Restore }
public sealed record UpdateProducts(long[] Ids, ProductAction Action, string? Actor) : IRequest<int>;
public sealed record QueueProductCheck(long Id, string? Actor) : IRequest;
public sealed record QueueDiscovery(string BrandId) : IRequest<Guid>;
public sealed record AddProductLink(string? BrandId, string? Url) : IRequest<ProductView>;
public sealed record PreviewProduct(long Id) : IRequest<object>;

public sealed partial class CatalogCommands(AppDbContext db, IProductSource source) :
    IRequestHandler<UpdateProducts, int>, IRequestHandler<QueueProductCheck>,
    IRequestHandler<QueueDiscovery, Guid>, IRequestHandler<AddProductLink, ProductView>,
    IRequestHandler<PreviewProduct, object>
{
    public async Task<int> Handle(UpdateProducts request, CancellationToken ct)
    {
        if (request.Ids.Length is < 1 or > 1000 || request.Ids.Any(x => x <= 0))
            throw new PanelException(422, "بین ۱ تا ۱۰۰۰ شناسهٔ معتبر محصول وارد کنید.");
        var ids = request.Ids.Distinct().ToArray();
        var timestamp = DateTime.UtcNow;
        var query = db.Products.Where(x => ids.Contains(x.PublicId));
        switch (request.Action)
        {
            case ProductAction.Delete:
                return await query.ExecuteUpdateAsync(set => set
                    .SetProperty(x => x.IsDeleted, true).SetProperty(x => x.DeletedAt, timestamp)
                    .SetProperty(x => x.IsTracked, false).SetProperty(x => x.NextCheckAt, (DateTime?)null)
                    .SetProperty(x => x.LeaseUntil, (DateTime?)null)
                    .SetProperty(x => x.ModifiedAt, timestamp).SetProperty(x => x.ModifiedBy, request.Actor), ct);
            case ProductAction.Restore:
                return await query.IgnoreQueryFilters().Where(x => x.IsDeleted && !x.Brand.IsDeleted)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsDeleted, false)
                    .SetProperty(x => x.DeletedAt, (DateTime?)null)
                    .SetProperty(x => x.ModifiedAt, timestamp).SetProperty(x => x.ModifiedBy, request.Actor), ct);
            default:
                var track = request.Action == ProductAction.Track;
                return await query.ExecuteUpdateAsync(set => set.SetProperty(x => x.IsTracked, track)
                    .SetProperty(x => x.NextCheckAt, x => track ? timestamp : x.NextCheckAt)
                    .SetProperty(x => x.ModifiedAt, timestamp).SetProperty(x => x.ModifiedBy, request.Actor), ct);
        }
    }

    public async Task Handle(QueueProductCheck request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var query = db.Products.Where(x => x.PublicId == request.Id);
        if (await query.Where(x => x.IsTracked).ExecuteUpdateAsync(set => set.SetProperty(x => x.NextCheckAt, now)
            .SetProperty(x => x.ModifiedAt, now).SetProperty(x => x.ModifiedBy, request.Actor), ct) != 0)
            return;
        if (!await query.AnyAsync(ct))
            throw CatalogQueries.MissingProduct();
        throw new PanelException(422, "ابتدا پایش این محصول را فعال کنید.");
    }

    public Task<Guid> Handle(QueueDiscovery request, CancellationToken ct) => InTransaction(async () =>
    {
        var brandId = await db.Brands.Where(x => x.Slug == request.BrandId).Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(ct) ?? throw MissingBrand();
        var existing = await db.DiscoveryTasks
            .Where(x => x.BrandId == brandId && (x.Status == DiscoveryTaskStatus.Queued || x.Status == DiscoveryTaskStatus.Running))
            .OrderBy(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (existing is Guid id)
            return id;
        var task = new DiscoveryTask { BrandId = brandId };
        db.DiscoveryTasks.Add(task);
        await db.SaveChangesAsync(ct);
        return task.Id;
    }, ct);

    public async Task<ProductView> Handle(AddProductLink request, CancellationToken ct)
    {
        var sourceId = ProductLinks.SourceId(request.Url);
        var brand = await db.Brands.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == request.BrandId, ct)
            ?? throw MissingBrand();
        var exists = await db.Products.IgnoreQueryFilters().AnyAsync(x => x.SourceId == sourceId, ct);
        var snapshot = exists ? null : await ReadSource(sourceId, ct);
        return await InTransaction(async () =>
        {
            if (!await db.Brands.AnyAsync(x => x.Id == brand.Id, ct))
                throw MissingBrand();
            var product = await db.Products.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.SourceId == sourceId, ct);
            var now = DateTime.UtcNow;
            if (product != null)
            {
                var restored = product.IsDeleted;
                var brandChanged = product.BrandId != brand.Id;
                product.Restore();
                product.BrandId = brand.Id;
                product.Url = ProductLinks.Canonical(sourceId);
                product.IsTracked = true;
                product.NextCheckAt = now;
                await db.SaveChangesAsync(ct);
                return ProductView.From(product, brand.Slug) with
                {
                    AlreadyExists = true, BrandChanged = brandChanged, Restored = restored,
                    Message = restored ? "محصول حذف‌شده بازیابی شد و پایش آن فعال شد."
                        : "این محصول قبلاً ثبت شده بود؛ پایش آن برای برند انتخاب‌شده فعال شد."
                };
            }

            if (snapshot == null)
                throw new PanelException(409, "وضعیت محصول تغییر کرد؛ دوباره تلاش کنید.");

            var intervalText = await db.AppSettings.Where(x => x.Key == "interval_minutes").Select(x => x.Value)
                .SingleOrDefaultAsync(ct);
            var interval = int.TryParse(intervalText, out var minutes) && minutes is >= 1 and <= 10080 ? minutes : 60;
            product = new Product
            {
                BrandId = brand.Id, SourceId = sourceId, Title = snapshot.Title, Url = ProductLinks.Canonical(sourceId),
                ImageUrl = snapshot.ImageUrl, IsTracked = true, LastSuccessAt = now, NextCheckAt = now.AddMinutes(interval)
            };
            db.Products.Add(product);
            var sellerIds = snapshot.Offers.Select(x => x.SellerId).Distinct().ToArray();
            var sellers = await db.Sellers.Where(x => sellerIds.Contains(x.SourceId)).ToDictionaryAsync(x => x.SourceId, ct);
            foreach (var offer in snapshot.Offers)
            {
                if (!sellers.TryGetValue(offer.SellerId, out var seller))
                {
                    seller = new Seller { SourceId = offer.SellerId, Name = offer.SellerName };
                    db.Sellers.Add(seller);
                    sellers.Add(offer.SellerId, seller);
                }
                seller.Name = offer.SellerName;
                db.LatestOffers.Add(new LatestOffer
                {
                    Product = product, Seller = seller, OfferKey = offer.OfferKey, ComparisonKey = offer.ComparisonKey,
                    Variant = offer.Variant, Price = offer.Price, ListPrice = offer.ListPrice,
                    Available = offer.Available, ObservedAt = now
                });
                db.PriceObservations.Add(new PriceObservation
                {
                    Product = product, Seller = seller, OfferKey = offer.OfferKey, Variant = offer.Variant,
                    Price = offer.Price, ListPrice = offer.ListPrice, Available = offer.Available, ObservedAt = now
                });
            }
            db.Notifications.Add(new Notification
            {
                Product = product, BrandId = brand.Id, Kind = NotificationKind.ProductFound,
                Title = $"محصول با لینک به برند {brand.Name} اضافه و پایش شد: {product.Title}"
            });
            await db.SaveChangesAsync(ct);
            var normalized = Normalize($"{snapshot.Title} {snapshot.BrandTitle}");
            var matchingBrand = brand.Aliases.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(alias => normalized.Contains(Normalize(alias), StringComparison.Ordinal));
            return ProductView.From(product, brand.Slug) with
            {
                AlreadyExists = false, BrandChanged = false,
                BrandWarning = matchingBrand ? null : "برند انتخاب‌شده اعمال شد؛ نام برند در مشخصات منبع تطبیق خودکار نداشت.",
                Message = "محصول جدید به برند انتخاب‌شده اضافه شد و پایش آن فعال است."
            };
        }, ct);
    }

    public async Task<object> Handle(PreviewProduct request, CancellationToken ct)
    {
        var sourceId = await db.Products.Where(x => x.PublicId == request.Id).Select(x => x.SourceId)
            .SingleOrDefaultAsync(ct) ?? throw CatalogQueries.MissingProduct();
        var snapshot = await ReadSource(sourceId, ct);
        var now = DateTime.UtcNow;
        return new
        {
            product_id = request.Id, observed_at = now, mode = "live", availability = snapshot.Availability,
            seller_count = snapshot.Offers.Select(x => x.SellerId).Distinct().Count(),
            offers = snapshot.Offers.OrderBy(x => x.Price == null).ThenBy(x => x.Price).Select((x, i) => new
            {
                id = -(i + 1), x.SellerId, x.SellerName, x.OfferKey, x.Variant, x.Price, x.ListPrice,
                x.Available, observed_at = now, missing = false
            })
        };
    }

    private async Task<SourceProduct> ReadSource(string id, CancellationToken ct)
    {
        try { return await source.GetProductAsync(id, ct); }
        catch (ProductSourceException ex) { throw new PanelException(502, ex.Message); }
    }

    private async Task<T> InTransaction<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var result = await operation();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (Exception ex) when ((ex as SqlException ?? ex.InnerException as SqlException) is
            { Number: 1205 or 2601 or 2627 })
        {
            throw new PanelException(409, "درخواست هم‌زمان دیگری ثبت شد؛ دوباره تلاش کنید.");
        }
    }

    private static PanelException MissingBrand() => new(404, "برند پیدا نشد.");
    private static string Normalize(string text) => Separators().Replace(
        text.ToLowerInvariant().Replace('ي', 'ی').Replace('ك', 'ک').Replace('آ', 'ا'), "");

    [GeneratedRegex(@"[\s\u200c\u200fـ_\-]+")]
    private static partial Regex Separators();
}
