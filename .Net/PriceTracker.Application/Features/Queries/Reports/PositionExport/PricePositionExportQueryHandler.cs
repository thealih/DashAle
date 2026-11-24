using MediatR;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Infrastracture.Reports;

namespace PriceTracker.Application.Features.Queries.Reports.PositionExport;

public sealed class PricePositionExportQueryHandler(PricePosition comparison) : IRequestHandler<PricePositionExportQuery>
{
    public async Task Handle(PricePositionExportQuery request, CancellationToken ct)
    {
        var result = await comparison.CompareAsync(
            request.BrandId, request.Search, request.Status, 1, request.Limit, ct);
        if (!result.Configured)
            throw new PanelException(422, "ابتدا فروشگاه پیش‌فرض را در تنظیمات انتخاب کنید.");
        if (result.Total > request.Limit)
            throw ReportRows.TooMany(request.Limit);

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

    private const string PositionTitle = "مقایسه فروشگاه ما";
}
