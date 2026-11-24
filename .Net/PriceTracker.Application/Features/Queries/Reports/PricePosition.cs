using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Monitoring;
using PriceTracker.Infrastracture;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Queries.Reports;

public sealed record PositionQuery(string? BrandId, string? Search, string Status, int Page, int PageSize)
    : IRequest<PositionResult>;

public sealed record PositionResult(bool Configured, OwnStoreView Store, IReadOnlyList<PositionRow> Items,
    int Total, IReadOnlyDictionary<string, int> Summary, int Products, int NotBestProducts, int Page, DateTime GeneratedAt);

public sealed record PositionRow(long ProductId, string ProductTitle, string BrandId, string BrandName,
    string Url, DateTime? LastSuccess, string? Error)
{
    public string Variant { get; init; } = "—";
    public long? OwnPrice { get; init; }
    public long? CompetitorPrice { get; init; }
    public string? CompetitorId { get; init; }
    public string? CompetitorName { get; init; }
    public long? Difference { get; init; }
    public long? TargetPrice { get; init; }
    public long? ReductionNeeded { get; init; }
    public DateTime? OwnObservedAt { get; init; }
    public DateTime? CompetitorObservedAt { get; init; }
    public string Status { get; init; } = "";
    public string StatusLabel => PricePosition.Labels[Status];
}

public sealed class PricePosition(AppDbContext db) : IRequestHandler<PositionQuery, PositionResult>
{
    internal static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["not_best"] = "قیمت ما بهترین نیست",
        ["best"] = "قیمت ما پایین‌تر است",
        ["tied"] = "هم‌قیمت ارزان‌ترین رقیب",
        ["no_competitor"] = "رقیب قابل مقایسه یافت نشد",
        ["missing_own"] = "فروشگاه ما در محصول یافت نشد",
        ["own_unavailable"] = "پیشنهاد ما موجود یا جاری نیست",
        ["needs_review"] = "نیازمند دریافت یا بررسی اطلاعات",
        ["not_monitored"] = "پایش غیرفعال"
    };

    public Task<PositionResult> Handle(PositionQuery request, CancellationToken ct)
    {
        _ = PanelPagination.Offset(request.Page, request.PageSize, 100);
        return CompareAsync(request, ct);
    }

    internal async Task<PositionResult> CompareAsync(PositionQuery request, CancellationToken ct)
    {
        if (request.Status != "all" && !Labels.ContainsKey(request.Status))
            throw new PanelException(422, "وضعیت مقایسه معتبر نیست.");
        if (request.Search is { Length: > 300 })
            throw new PanelException(422, "عبارت جست‌وجو حداکثر ۳۰۰ کاراکتر است.");

        var store = await OwnStore.LoadAsync(db, ct);
        var counts = Labels.Keys.ToDictionary(x => x, _ => 0);
        var now = DateTime.UtcNow;
        if (store.SellerId.Length == 0)
            return new(false, store, [], 0, counts, 0, 0, request.Page, now);

        var settings = await MonitoringSettings.LoadAsync(db, ct);
        var cutoff = now.AddMinutes(-settings.IntervalMinutes * 1.5);
        var selected = db.Products.AsNoTracking();
        if (!string.IsNullOrEmpty(request.BrandId))
            selected = selected.Where(x => x.Brand.Slug == request.BrandId);
        if (!string.IsNullOrEmpty(request.Search))
            selected = selected.Where(x => x.Title.Contains(request.Search));

        var matching = new List<PositionRow>();
        var products = 0;
        var notBestProducts = 0;
        var after = 0L;
        while (true)
        {
            // The monitoring writer locks the product before replacing its offers. Hold read locks
            // for one batch so the product timestamp and the offers describe the same collection.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var batch = await selected.Where(x => x.PublicId > after).OrderBy(x => x.PublicId).Take(250)
                .Select(x => new ProductData(x.Id, x.PublicId, x.Title, x.Brand.Slug, x.Brand.Name,
                    x.SourceId, x.Url, x.IsTracked, x.LastSuccessAt, x.Error)).ToListAsync(ct);
            if (batch.Count == 0)
            {
                await transaction.CommitAsync(ct);
                break;
            }
            var ids = batch.Where(x => x.IsTracked && x.LastSuccess >= cutoff && string.IsNullOrEmpty(x.Error))
                .Select(x => x.Id).ToArray();
            var offers = await db.LatestOffers.AsNoTracking().Where(x => ids.Contains(x.ProductId))
                .Select(x => new OfferData(x.ProductId, x.Seller.SourceId, x.Seller.Name, x.Variant,
                    x.ComparisonKey, x.Price, x.Available, x.ObservedAt, x.IsMissing, x.OfferKey)).ToListAsync(ct);
            await transaction.CommitAsync(ct);

            var lookup = offers.ToLookup(x => x.ProductId);
            foreach (var product in batch)
            {
                var needsPriceChange = false;
                foreach (var row in CompareProduct(product, lookup[product.Id], store, cutoff))
                {
                    counts[row.Status]++;
                    needsPriceChange |= row.Status == "not_best";
                    if (request.Status == "all" || request.Status == row.Status)
                        matching.Add(row);
                }
                if (needsPriceChange)
                    notBestProducts++;
            }
            products += batch.Count;
            after = batch[^1].PublicId;
        }

        var items = matching.OrderBy(x => x.Status != "not_best").ThenByDescending(x => x.Difference ?? 0)
            .ThenBy(x => x.ProductId).ThenBy(x => x.Variant, StringComparer.Ordinal)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToArray();
        return new(true, store, items, matching.Count, counts, products, notBestProducts, request.Page, now);
    }

    private static IEnumerable<PositionRow> CompareProduct(ProductData product, IEnumerable<OfferData> offers,
        OwnStoreView store, DateTime cutoff)
    {
        var row = new PositionRow(product.PublicId, product.Title, product.BrandId, product.BrandName,
            ProductLinks.Canonical(product.SourceId, product.Url), product.LastSuccess, product.Error);
        if (!product.IsTracked)
            return [row with { Status = "not_monitored" }];
        if (!string.IsNullOrEmpty(product.Error) || product.LastSuccess == null || product.LastSuccess < cutoff)
            return [row with { Status = "needs_review" }];

        var own = offers.Where(x => x.SellerId == store.SellerId).OrderBy(x => x.OfferKey, StringComparer.Ordinal).ToArray();
        if (own.Length == 0)
            return [row with { Status = "missing_own" }];
        var peers = offers.Where(x => x.SellerId != store.SellerId && !x.Missing && x.ObservedAt == product.LastSuccess)
            .ToArray();
        var unknownVariant = peers.Any(x => string.IsNullOrEmpty(x.ComparisonKey));
        var comparable = peers.ToLookup(x => x.ComparisonKey, StringComparer.Ordinal);

        return own.Select(x =>
        {
            var details = row with { Variant = x.Variant, OwnPrice = x.Price, OwnObservedAt = x.ObservedAt };
            if (x.Missing || x.ObservedAt != product.LastSuccess || x.Available == false || x.Price is not > 0)
                return details with { Status = "own_unavailable" };
            if (string.IsNullOrEmpty(x.ComparisonKey) || x.Available == null || unknownVariant)
                return details with { Status = "needs_review" };

            var alternatives = comparable[x.ComparisonKey];
            if (alternatives.Any(p => p.Available == null))
                return details with { Status = "needs_review" };
            var cheapest = alternatives.Where(p => p.Available == true && p.Price is > 0)
                .OrderBy(p => p.Price).ThenBy(p => p.SellerId, StringComparer.Ordinal).FirstOrDefault();
            if (cheapest == null)
                return details with { Status = "no_competitor" };
            var rival = cheapest.Price!.Value;
            var difference = x.Price.Value - rival;
            long? target = difference > 0 && rival > store.UndercutToman ? rival - store.UndercutToman : null;
            return details with
            {
                Status = difference > 0 ? "not_best" : difference < 0 ? "best" : "tied",
                CompetitorPrice = rival, CompetitorId = cheapest.SellerId, CompetitorName = cheapest.SellerName,
                CompetitorObservedAt = cheapest.ObservedAt, Difference = difference, TargetPrice = target,
                ReductionNeeded = target.HasValue ? x.Price.Value - target.Value : null
            };
        });
    }

    private sealed record ProductData(Guid Id, long PublicId, string Title, string BrandId, string BrandName,
        string SourceId, string Url, bool IsTracked, DateTime? LastSuccess, string? Error);

    private sealed record OfferData(Guid ProductId, string SellerId, string SellerName, string Variant,
        string? ComparisonKey, long? Price, bool? Available, DateTime ObservedAt, bool Missing, string OfferKey);
}
