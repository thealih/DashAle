using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Monitoring;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Infrastracture;

namespace PriceTracker.Api.Panel;

[ApiController, ApiVersionNeutral]
[Authorize(AuthenticationSchemes = PanelSecurity.Scheme)]
[Route("api")]
public sealed class MonitoringController(AppDbContext db) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public sealed record SettingsBody(
        [property: JsonPropertyName("interval_minutes")] int? IntervalMinutes,
        [property: JsonPropertyName("threshold_percent")] double? ThresholdPercent,
        [property: JsonPropertyName("retention_days")] int? RetentionDays,
        [property: JsonPropertyName("discovery_hours")] int? DiscoveryHours);

    [HttpGet("jobs")]
    public async Task<IActionResult> Jobs(CancellationToken ct)
    {
        var tasks = await db.DiscoveryTasks.AsNoTracking().OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id).Take(30).Select(x => new
            {
                x.Id, x.Kind, x.Status, x.CreatedAt, x.FinishedAt, x.LeaseUntil, x.Message,
                BrandId = x.Brand == null ? null : x.Brand.Slug
            }).ToListAsync(ct);
        return new JsonResult(tasks.Select(x => new
        {
            x.Id, kind = x.Kind.ToString().ToLowerInvariant(), status = x.Status.ToString().ToLowerInvariant(),
            x.BrandId, x.CreatedAt, x.FinishedAt, x.LeaseUntil, x.Message
        }), Json);
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken ct) =>
        new JsonResult(await MonitoringSettings.LoadAsync(db, ct), Json);

    [HttpPut("settings"), Authorize(Policy = PanelSecurity.Admin)]
    public async Task<IActionResult> SaveSettings(SettingsBody? input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input?.IntervalMinutes is not (>= 5 and <= 1440)
            || input.ThresholdPercent is not (>= 0 and <= 1000) || input.RetentionDays is not (>= 1 and <= 365)
            || input.DiscoveryHours is not (>= 1 and <= 168))
            return UnprocessableEntity(new { detail = "مقادیر تنظیمات پایش معتبر نیستند." });

        var settings = new MonitoringSettings(input.IntervalMinutes.Value, input.ThresholdPercent.Value,
            input.RetentionDays.Value, input.DiscoveryHours.Value);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var old = await MonitoringSettings.LoadAsync(db, ct);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["interval_minutes"] = settings.IntervalMinutes.ToString(CultureInfo.InvariantCulture),
            ["threshold_percent"] = settings.ThresholdPercent.ToString(CultureInfo.InvariantCulture),
            ["retention_days"] = settings.RetentionDays.ToString(CultureInfo.InvariantCulture),
            ["discovery_hours"] = settings.DiscoveryHours.ToString(CultureInfo.InvariantCulture)
        };
        var keys = values.Keys.ToArray();
        var existing = await db.AppSettings.IgnoreQueryFilters().Where(x => keys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, ct);
        foreach (var (key, value) in values)
        {
            if (!existing.TryGetValue(key, out var setting))
                db.AppSettings.Add(new AppSetting { Key = key, Value = value });
            else
            {
                setting.Value = value;
                setting.IsDeleted = false;
            }
        }
        await db.SaveChangesAsync(ct);
        if (old.IntervalMinutes != settings.IntervalMinutes)
        {
            var now = DateTime.UtcNow;
            await db.Products.Where(x => x.IsTracked && x.LastSuccessAt != null && x.Error == null
                && x.LeaseUntil == null && x.NextCheckAt > now).ExecuteUpdateAsync(set => set
                .SetProperty(x => x.NextCheckAt, x => x.LastSuccessAt!.Value.AddMinutes(settings.IntervalMinutes)), ct);
        }
        await transaction.CommitAsync(ct);
        return new JsonResult(settings, Json);
    }
}
