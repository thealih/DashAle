using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Monitoring;

public sealed record MonitoringSettings(int IntervalMinutes, double ThresholdPercent, int RetentionDays, int DiscoveryHours)
{
    public static async Task<MonitoringSettings> LoadAsync(AppDbContext db, CancellationToken ct)
    {
        var values = await db.AppSettings.AsNoTracking()
            .Where(x => x.Key == "interval_minutes" || x.Key == "threshold_percent"
                || x.Key == "retention_days" || x.Key == "discovery_hours")
            .ToDictionaryAsync(x => x.Key, x => x.Value, ct);
        int Integer(string key, int fallback, int min, int max) =>
            int.TryParse(values.GetValueOrDefault(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                && value >= min && value <= max ? value : fallback;
        var threshold = double.TryParse(values.GetValueOrDefault("threshold_percent"), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var percent) && percent is >= 0 and <= 1000 ? percent : 0;
        return new(Integer("interval_minutes", 60, 5, 1440), threshold, Integer("retention_days", 30, 1, 365),
            Integer("discovery_hours", 24, 1, 168));
    }
}
