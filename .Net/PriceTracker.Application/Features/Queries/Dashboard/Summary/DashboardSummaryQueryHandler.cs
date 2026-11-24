using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Queries.Dashboard.Summary;

public sealed class DashboardSummaryQueryHandler(AppDbContext db) : IRequestHandler<DashboardSummaryQuery, object>
{
    public async Task<object> Handle(DashboardSummaryQuery request, CancellationToken ct)
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
}
