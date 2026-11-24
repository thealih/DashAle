using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Monitoring;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.IServices;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;
using System.Text.RegularExpressions;

namespace PriceTracker.Application.Features.Commands.Catalog.AddLink;

public sealed partial class ProductAddLinkCommandHandler(AppDbContext db, IProductSource source) : IRequestHandler<ProductAddLinkCommand, ProductView>
{
    public async Task<ProductView> Handle(ProductAddLinkCommand request, CancellationToken ct)
    {
        var sourceId = ProductLinks.SourceId(request.Url);
        var brand = await db.Brands.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == request.BrandId, ct)
            ?? throw MissingBrand();
        var exists = await db.Products.IgnoreQueryFilters().AnyAsync(x => x.SourceId == sourceId, ct);
        var snapshot = exists ? null : await ProductSourceReader.ReadAsync(source, sourceId, ct);
        return await PanelWriteTransaction.RunAsync(db, async () =>
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

            var settings = await MonitoringSettings.LoadAsync(db, ct);
            product = new Product
            {
                BrandId = brand.Id, SourceId = sourceId, Title = snapshot.Title, Url = ProductLinks.Canonical(sourceId),
                ImageUrl = snapshot.ImageUrl, IsTracked = true, NextCheckAt = now.AddMinutes(settings.IntervalMinutes)
            };
            db.Products.Add(product);
            await PriceSnapshot.ApplyAsync(db, product, snapshot.Offers, now, settings.ThresholdPercent, ct);
            product.LastSuccessAt = now;
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

    private static PanelException MissingBrand() => new(404, "برند پیدا نشد.");

    private static string Normalize(string text) => Separators().Replace(
        text.ToLowerInvariant().Replace('ي', 'ی').Replace('ك', 'ک').Replace('آ', 'ا'), "");

    [GeneratedRegex(@"[\s\u200c\u200fـ_\-]+")]
    private static partial Regex Separators();
}
