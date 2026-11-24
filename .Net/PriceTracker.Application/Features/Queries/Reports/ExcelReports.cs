using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;
using PriceTracker.Infrastracture.Reports;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Queries.Reports;

public sealed record ExportHistory(Stream Output, string? BrandId, long? ProductId, string? SellerId,
    DateTime? Start, DateTime? End, bool IncludeHistory, int Limit) : IRequest;

public sealed record ExportPricePosition(Stream Output, string? BrandId, string? Search, string Status, int Limit)
    : IRequest;

public sealed class ExcelReports(AppDbContext db, PricePosition comparison) :
    IRequestHandler<ExportHistory>, IRequestHandler<ExportPricePosition>
{
    private const string SummaryTitle = "خلاصه مدیریتی";
    private const string LatestTitle = "آخرین قیمت‌ها";
    private const string ChangesTitle = "تغییرات قیمت";
    private const string HistoryTitle = "تاریخچه";
    private const string PositionTitle = "مقایسه فروشگاه ما";

    private static readonly string[] OfferHeaders = ["برند", "محصول", "شناسهٔ منبع", "فروشنده", "شناسهٔ فروشنده",
        "گونه / گارانتی", "قیمت فروش (تومان)", "قیمت پایه (تومان)", "وضعیت موجودی", "زمان مشاهده (تهران)"];
    private static readonly int[] OfferWidths = [18, 65, 20, 30, 22, 45, 24, 24, 22, 28];

    public async Task Handle(ExportHistory request, CancellationToken ct)
    {
        if (request.Start >= request.End)
            throw new PanelException(422, "پایان بازه باید بعد از شروع باشد.");

        var products = db.Products.AsNoTracking();
        if (!string.IsNullOrEmpty(request.BrandId))
            products = products.Where(p => p.Brand.Slug == request.BrandId);
        if (request.ProductId is long id)
            products = products.Where(p => p.PublicId == id);
        var latest = from offer in db.LatestOffers.AsNoTracking()
                     join p in products on offer.ProductId equals p.Id
                     select new
                     {
                         offer.Id, Brand = p.Brand.Name, p.Title, p.SourceId, p.Url, p.Error,
                         Seller = offer.Seller.Name, SellerId = offer.Seller.SourceId,
                         offer.Variant, offer.Price, offer.ListPrice, offer.Available, offer.ObservedAt, offer.IsMissing
                     };
        var changes = from notice in db.Notifications.AsNoTracking()
                      join p in products on notice.ProductId equals (Guid?)p.Id
                      where notice.Kind == NotificationKind.PriceUp || notice.Kind == NotificationKind.PriceDown
                      select new
                      {
                          notice.PublicId, Brand = p.Brand.Name, ProductTitle = p.Title, Description = notice.Title,
                          Seller = notice.Seller == null ? null : notice.Seller.Name,
                          SellerId = notice.Seller == null ? null : notice.Seller.SourceId,
                          notice.OldPrice, notice.NewPrice, notice.Percent, notice.PreviousObservedAt, notice.CreatedAt
                      };
        var history = from observation in db.PriceObservations.AsNoTracking()
                      join p in products on observation.ProductId equals p.Id
                      select new
                      {
                          observation.Id, Brand = p.Brand.Name, p.Title, p.SourceId, p.Url,
                          Seller = observation.Seller.Name, SellerId = observation.Seller.SourceId,
                          observation.Variant, observation.Price, observation.ListPrice, observation.Available, observation.ObservedAt
                      };
        if (!string.IsNullOrEmpty(request.SellerId))
        {
            latest = latest.Where(x => x.SellerId == request.SellerId);
            changes = changes.Where(x => x.SellerId == request.SellerId);
            history = history.Where(x => x.SellerId == request.SellerId);
        }
        if (request.Start is DateTime start)
        {
            changes = changes.Where(x => x.CreatedAt >= start);
            history = history.Where(x => x.ObservedAt >= start);
        }
        if (request.End is DateTime end)
        {
            changes = changes.Where(x => x.CreatedAt < end);
            history = history.Where(x => x.ObservedAt < end);
        }

        var latestCount = await latest.Take(request.Limit + 1).CountAsync(ct);
        var changeCount = await changes.Take(request.Limit + 1).CountAsync(ct);
        var historyCount = request.IncludeHistory ? await history.Take(request.Limit + 1).CountAsync(ct) : 0;
        if (latestCount + changeCount + historyCount > request.Limit)
            throw TooManyRows(request.Limit);

        using var book = new XlsxWorkbook(request.Output, DateTime.UtcNow);
        var summarySheet = book.Register(SummaryTitle);
        var latestSheet = book.Register(LatestTitle);
        var changesSheet = book.Register(ChangesTitle);
        var historySheet = request.IncludeHistory ? book.Register(HistoryTitle) : null;
        var remaining = request.Limit;

        latestCount = await book.WriteSheetAsync(latestSheet, LatestTitle,
            [.. OfferHeaders, "وضعیت دریافت", "لینک منبع"], [.. OfferWidths, 30, 60], async rows =>
            {
                await foreach (var o in latest.OrderBy(x => x.Title).ThenBy(x => x.Seller).ThenBy(x => x.Id)
                    .AsAsyncEnumerable().WithCancellation(ct))
                {
                    CountRow();
                    rows.Write(o.Brand, o.Title, o.SourceId, o.Seller, o.SellerId, o.Variant, o.Price, o.ListPrice,
                        Stock(o.Available), o.ObservedAt, o.IsMissing ? "در آخرین دریافت دیده نشد"
                            : o.Error != null ? "خطای آخرین پایش" : "ثبت موفق", ProductLinks.Canonical(o.SourceId, o.Url));
                }
            });

        changeCount = await book.WriteSheetAsync(changesSheet, ChangesTitle,
            ["برند", "محصول", "فروشنده", "شرح / گونه", "قیمت قبلی (تومان)", "قیمت جدید (تومان)",
                "اختلاف (تومان)", "درصد تغییر", "مشاهده قبلی (تهران)", "تشخیص تغییر (تهران)"],
            [18, 65, 30, 70, 24, 24, 24, 18, 28, 28], async rows =>
            {
                await foreach (var n in changes.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.PublicId)
                    .AsAsyncEnumerable().WithCancellation(ct))
                {
                    CountRow();
                    var percent = n.Percent is double value && double.IsFinite(value) ? Math.Round(value, 2) : (double?)null;
                    rows.Write(n.Brand, n.ProductTitle, n.Seller, n.Description, n.OldPrice, n.NewPrice,
                        (decimal?)n.NewPrice - n.OldPrice, percent, n.PreviousObservedAt, n.CreatedAt);
                }
            });

        if (historySheet != null)
            historyCount = await book.WriteSheetAsync(historySheet, HistoryTitle,
                [.. OfferHeaders, "لینک منبع"], [.. OfferWidths, 60], async rows =>
                {
                    await foreach (var o in history.OrderByDescending(x => x.ObservedAt).ThenByDescending(x => x.Id)
                        .AsAsyncEnumerable().WithCancellation(ct))
                    {
                        CountRow();
                        rows.Write(o.Brand, o.Title, o.SourceId, o.Seller, o.SellerId, o.Variant, o.Price, o.ListPrice,
                            Stock(o.Available), o.ObservedAt, ProductLinks.Canonical(o.SourceId, o.Url));
                    }
                });

        await book.WriteSheetAsync(summarySheet, SummaryTitle, ["شاخص", "مقدار"], [47, 75], rows =>
        {
            rows.Write("برند", string.IsNullOrEmpty(request.BrandId) ? "همهٔ برندها" : request.BrandId);
            rows.Write("شناسهٔ محصول در پنل", request.ProductId);
            rows.Write("شناسهٔ فروشنده", request.SellerId);
            rows.Write("شروع بازهٔ تاریخچه (تهران)", (object?)request.Start ?? "ابتدای سابقه");
            rows.Write("پایان انحصاری بازهٔ تاریخچه (تهران)", (object?)request.End ?? "تا زمان تهیهٔ گزارش");
            rows.Write("ردیف آخرین پیشنهادهای فروش", latestCount);
            rows.Write("تغییرات قیمت در بازه", changeCount);
            rows.Write("مشاهدات در بازه", request.IncludeHistory ? historyCount : "تاریخچه درخواست نشده");
            rows.Write("مبنای آخرین قیمت‌ها", "آخرین مشاهدهٔ ثبت‌شده، مستقل از بازهٔ تاریخچه");
            return Task.CompletedTask;
        });

        void CountRow()
        {
            if (--remaining < 0)
                throw TooManyRows(request.Limit);
        }
    }

    public async Task Handle(ExportPricePosition request, CancellationToken ct)
    {
        var result = await comparison.CompareAsync(
            new PositionQuery(request.BrandId, request.Search, request.Status, 1, request.Limit), ct);
        if (!result.Configured)
            throw new PanelException(422, "ابتدا فروشگاه پیش‌فرض را در تنظیمات انتخاب کنید.");
        if (result.Total > request.Limit)
            throw TooManyRows(request.Limit);

        using var book = new XlsxWorkbook(request.Output, result.GeneratedAt);
        await book.WriteSheetAsync(book.Register(PositionTitle), PositionTitle,
            ["برند", "محصول", "گونه / گارانتی", "فروشگاه ما", "قیمت ما (تومان)", "ارزان‌ترین رقیب",
                "قیمت رقیب (تومان)", "اختلاف با رقیب (تومان)", "قیمت پیشنهادی (تومان)", "کاهش لازم (تومان)",
                "وضعیت", "مشاهده قیمت ما (تهران)", "مشاهده قیمت رقیب (تهران)", "لینک محصول"],
            [20, 65, 45, 30, 23, 32, 23, 24, 25, 23, 42, 28, 28, 65], rows =>
            {
                foreach (var row in result.Items)
                {
                    ct.ThrowIfCancellationRequested();
                    rows.Write(row.BrandName, row.ProductTitle, row.Variant, result.Store.SellerName, row.OwnPrice,
                        row.CompetitorName, row.CompetitorPrice, row.Difference, row.TargetPrice, row.ReductionNeeded,
                        row.StatusLabel, row.OwnObservedAt, row.CompetitorObservedAt, row.Url);
                }
                return Task.CompletedTask;
            });
    }

    private static string Stock(bool? value) => value switch { true => "موجود", false => "ناموجود", null => "نامشخص" };

    private static PanelException TooManyRows(int limit) =>
        new(413, $"گزارش بیش از {limit:N0} ردیف است. بازه یا فیلتر را محدود کنید.");
}
