using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Panel;

public sealed record BrandList : IRequest<object>;
public sealed record DashboardSummary : IRequest<object>;
public sealed record ProductList(string? BrandId, string? Search, bool? Tracked, bool Deleted,
    int Page, int PageSize) : IRequest<object>;
public sealed record ProductDetail(long Id) : IRequest<ProductView>;
public sealed record ProductHistory(long Id, string? SellerId, DateTime? Start, DateTime? End,
    int Page, int PageSize) : IRequest<object>;
public sealed record ProductTimeline(long Id, string? SellerId) : IRequest<object>;

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

public sealed class CatalogQueries(AppDbContext db) : IRequestHandler<BrandList, object>,
    IRequestHandler<DashboardSummary, object>, IRequestHandler<ProductList, object>,
    IRequestHandler<ProductDetail, ProductView>, IRequestHandler<ProductHistory, object>,
    IRequestHandler<ProductTimeline, object>
{
    public async Task<object> Handle(BrandList request, CancellationToken ct)
    {
        var brands = await db.Brands.AsNoTracking().OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(b => new
            {
                id = b.Slug, b.Name, english = b.EnglishName, b.Aliases,
                last_discovered = b.LastDiscoveredAt, b.DiscoveryError,
                total = db.Products.Count(x => x.BrandId == b.Id),
                tracked = db.Products.Count(x => x.BrandId == b.Id && x.IsTracked),
                changes = db.Notifications.Count(x => x.BrandId == b.Id &&
                    (x.Kind == NotificationKind.PriceDown || x.Kind == NotificationKind.PriceUp)),
                task = db.DiscoveryTasks.Where(x => x.BrandId == b.Id).OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id).Select(x => new
                    {
                        x.Id, x.Kind, x.Status, x.CreatedAt, x.FinishedAt, x.LeaseUntil, x.Message
                    }).FirstOrDefault()
            }).ToListAsync(ct);

        return brands.Select(b => new
        {
            b.id, b.Name, b.english, b.Aliases, b.last_discovered, b.DiscoveryError, b.total, b.tracked, b.changes,
            job = b.task is null ? null : new
            {
                b.task.Id, brand_id = b.id, kind = b.task.Kind.ToString().ToLowerInvariant(),
                status = b.task.Status.ToString().ToLowerInvariant(), b.task.CreatedAt, b.task.FinishedAt,
                b.task.LeaseUntil, b.task.Message
            }
        });
    }

    public async Task<object> Handle(DashboardSummary request, CancellationToken ct)
    {
        var tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        var midnight = TimeZoneInfo.ConvertTimeToUtc(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tehran).Date, tehran);
        var settings = await db.AppSettings.AsNoTracking().ToDictionaryAsync(x => x.Key, x => x.Value, ct);
        var products = db.Products.AsNoTracking();
        return new
        {
            products = await products.CountAsync(ct),
            tracked = await products.CountAsync(x => x.IsTracked, ct),
            sellers = await db.LatestOffers.Where(x => x.Product.IsTracked && !x.IsMissing)
                .Select(x => x.SellerId).Distinct().CountAsync(ct),
            changes_today = await db.Notifications.CountAsync(x => x.CreatedAt >= midnight
                && (x.Kind == NotificationKind.PriceUp || x.Kind == NotificationKind.PriceDown), ct),
            unread = await db.Notifications.CountAsync(x => !x.IsRead && x.IsAlert, ct),
            errors = await products.CountAsync(x => x.IsTracked && x.Error != null, ct),
            last_success = await products.MaxAsync(x => x.LastSuccessAt, ct),
            heartbeat = settings.GetValueOrDefault("worker_heartbeat"),
            settings = new
            {
                interval_minutes = Number("interval_minutes", 60),
                threshold_percent = Number("threshold_percent", 0),
                retention_days = Number("retention_days", 30),
                discovery_hours = Number("discovery_hours", 24)
            },
            mode = "live"
        };

        double Number(string key, double fallback) =>
            double.TryParse(settings.GetValueOrDefault(key), System.Globalization.CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value) ? value : fallback;
    }

    public async Task<object> Handle(ProductList request, CancellationToken ct)
    {
        var offset = Offset(request.Page, request.PageSize, 100);
        IQueryable<Product> query = db.Products.AsNoTracking();
        if (request.Deleted)
            query = query.IgnoreQueryFilters().Where(x => x.IsDeleted && !x.Brand.IsDeleted);
        if (!string.IsNullOrEmpty(request.BrandId))
            query = query.Where(x => x.Brand.Slug == request.BrandId);
        if (request.Tracked is bool tracked)
            query = query.Where(x => x.IsTracked == tracked);
        if (!string.IsNullOrEmpty(request.Search))
            query = query.Where(x => x.Title.Contains(request.Search));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.IsTracked).ThenByDescending(x => x.CreatedAt)
            .ThenBy(x => x.PublicId).Skip(offset).Take(request.PageSize).Select(p => new
            {
                Product = p, Slug = p.Brand.Slug,
                Price = db.LatestOffers.IgnoreQueryFilters().Where(x => x.ProductId == p.Id && !x.IsDeleted
                    && !x.Seller.IsDeleted && !x.IsMissing && x.Available != false).Min(x => x.Price),
                Sellers = db.LatestOffers.IgnoreQueryFilters().Where(x => x.ProductId == p.Id && !x.IsDeleted
                    && !x.Seller.IsDeleted && !x.IsMissing).Select(x => x.SellerId).Distinct().Count()
            }).ToListAsync(ct);
        return new
        {
            items = rows.Select(x => ProductView.From(x.Product, x.Slug) with
            {
                MinPrice = x.Price,
                SellerCount = x.Product.LastSuccessAt != null || x.Sellers > 0 ? x.Sellers : null
            }),
            total, page = request.Page, page_size = request.PageSize
        };
    }

    public async Task<ProductView> Handle(ProductDetail request, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().Include(x => x.Brand)
            .SingleOrDefaultAsync(x => x.PublicId == request.Id, ct) ?? throw MissingProduct();
        var offers = await db.LatestOffers.Where(x => x.ProductId == product.Id)
            .OrderBy(x => x.IsMissing).ThenBy(x => x.Price).ThenBy(x => x.Id)
            .Select(x => new OfferView(x.Id, x.Seller.SourceId, x.Seller.Name, x.OfferKey, x.Variant,
                x.Price, x.ListPrice, x.Available, x.ObservedAt, x.IsMissing, x.ComparisonKey))
            .ToListAsync(ct);
        return ProductView.From(product, product.Brand.Slug) with { Offers = offers };
    }

    public async Task<object> Handle(ProductHistory request, CancellationToken ct)
    {
        var offset = Offset(request.Page, request.PageSize, 500);
        var query = await HistoryQuery(request.Id, request.SellerId, ct);
        if (request.Start is DateTime start)
            query = query.Where(x => x.ObservedAt >= start);
        if (request.End is DateTime end)
            query = query.Where(x => x.ObservedAt < end);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.ObservedAt).ThenByDescending(x => x.Id)
            .Skip(offset).Take(request.PageSize)
            .Select(x => new OfferView(x.Id, x.Seller.SourceId, x.Seller.Name, x.OfferKey, x.Variant,
                x.Price, x.ListPrice, x.Available, x.ObservedAt, false, null)).ToListAsync(ct);
        return new { items, total, page = request.Page };
    }

    public async Task<object> Handle(ProductTimeline request, CancellationToken ct)
    {
        var query = await HistoryQuery(request.Id, request.SellerId, ct);
        var points = await query.Where(x => x.Price != null && x.Available != false)
            .GroupBy(x => x.ObservedAt).Select(x => new { time = x.Key, price = x.Min(p => p.Price) })
            .OrderByDescending(x => x.time).Take(168).ToListAsync(ct);
        points.Reverse();
        return points;
    }

    private async Task<IQueryable<PriceObservation>> HistoryQuery(long publicId, string? sellerId, CancellationToken ct)
    {
        var productId = await db.Products.Where(x => x.PublicId == publicId).Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(ct) ?? throw MissingProduct();
        var query = db.PriceObservations.AsNoTracking().Where(x => x.ProductId == productId);
        return string.IsNullOrEmpty(sellerId) ? query : query.Where(x => x.Seller.SourceId == sellerId);
    }

    private static int Offset(int page, int size, int maxSize)
    {
        if (page < 1 || size < 1 || size > maxSize || (long)(page - 1) * size > int.MaxValue)
            throw new PanelException(422, "شماره یا اندازهٔ صفحه معتبر نیست.");
        return (page - 1) * size;
    }

    internal static PanelException MissingProduct() => new(404, "محصول پیدا نشد یا حذف شده است.");
}
