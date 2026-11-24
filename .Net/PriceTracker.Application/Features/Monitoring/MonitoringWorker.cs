using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.IServices;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Monitoring;

public sealed class MonitoringWorker(AppDbContext db, IProductSource products,
    IBrandProductSource discovery, ILogger<MonitoringWorker> logger)
{
    public async Task RunPricesAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await RecordHeartbeatAsync("worker_heartbeat", now, ct);
        var lastHeartbeat = now;
        var candidates = await DueProducts(now).OrderBy(x => x.NextCheckAt).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.SourceId }).Take(10).ToListAsync(ct);
        foreach (var product in candidates)
        {
            now = DateTime.UtcNow;
            if (now - lastHeartbeat >= TimeSpan.FromSeconds(30))
            {
                await RecordHeartbeatAsync("worker_heartbeat", now, ct);
                lastHeartbeat = now;
            }
            var lease = now.AddMinutes(5);
            if (await DueProducts(now).Where(x => x.Id == product.Id).ExecuteUpdateAsync(set => set
                    .SetProperty(x => x.LeaseUntil, lease).SetProperty(x => x.LastAttemptAt, now), ct) == 0)
                continue;

            try
            {
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
                budget.CancelAfter(TimeSpan.FromSeconds(45));
                var snapshot = await products.GetProductAsync(product.SourceId, budget.Token);
                await CompletePriceAsync(product.Id, lease, snapshot, null, ct);
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                db.ChangeTracker.Clear();
                var detail = SourceMessage(error, "پایش محصول ناموفق بود؛ لاگ سرور را بررسی کنید.");
                logger.LogWarning(error, "Price check failed for {ProductId}", product.Id);
                await CompletePriceAsync(product.Id, lease, null, detail, ct);
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    private IQueryable<Product> DueProducts(DateTime now) => db.Products.Where(x => x.IsTracked
        && (x.NextCheckAt == null || x.NextCheckAt <= now) && (x.LeaseUntil == null || x.LeaseUntil <= now));

    private async Task CompletePriceAsync(Guid id, DateTime lease, SourceProduct? snapshot, string? error,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var now = DateTime.UtcNow;
        // The conditional write fences an expired worker and locks the row against concurrent archive/stop.
        var acquired = await db.Products.Where(x => x.Id == id && x.IsTracked && x.LeaseUntil == lease
            && x.LeaseUntil > now).ExecuteUpdateAsync(set => set.SetProperty(x => x.LeaseUntil, (DateTime?)null), ct);
        if (acquired == 0)
            return;

        var product = await db.Products.SingleAsync(x => x.Id == id, ct);
        var requestedDuringFetch = product.NextCheckAt > product.LastAttemptAt;
        if (snapshot != null)
        {
            var config = await MonitoringSettings.LoadAsync(db, ct);
            await PriceSnapshot.ApplyAsync(db, product, snapshot.Offers, now, config.ThresholdPercent, ct);
            product.LastSuccessAt = now;
            product.Error = null;
            product.FailureCount = 0;
            if (!requestedDuringFetch)
                product.NextCheckAt = now.AddMinutes(config.IntervalMinutes);
        }
        else
        {
            if (product.Error == null)
                db.Notifications.Add(new Notification
                {
                    Kind = NotificationKind.Error, Product = product, BrandId = product.BrandId,
                    Title = $"پایش «{product.Title}» ناموفق بود: {error}", CreatedAt = now
                });
            product.Error = error;
            if (product.FailureCount < int.MaxValue)
                product.FailureCount++;
            if (!requestedDuringFetch)
                product.NextCheckAt = now.AddMinutes(Math.Min(360, 5 * Math.Pow(2, Math.Min(product.FailureCount - 1, 7))));
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RunDiscoveryAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await RecordHeartbeatAsync("discovery_heartbeat", now, ct);
        var candidate = await DueDiscovery(now).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Brand!.Aliases }).FirstOrDefaultAsync(ct);
        if (candidate == null)
            return;

        var lease = now.AddMinutes(15);
        if (await DueDiscovery(now).Where(x => x.Id == candidate.Id).ExecuteUpdateAsync(set => set
                .SetProperty(x => x.Status, DiscoveryTaskStatus.Running)
                .SetProperty(x => x.LeaseUntil, lease)
                .SetProperty(x => x.Message, "جست‌وجوی محصولات در حال انجام است."), ct) == 0)
            return;

        try
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(TimeSpan.FromMinutes(12));
            var results = await discovery.DiscoverAsync(candidate.Aliases, budget.Token);
            await CompleteDiscoveryAsync(candidate.Id, lease, results, null, ct);
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            db.ChangeTracker.Clear();
            var detail = SourceMessage(error, "جست‌وجوی برند ناموفق بود؛ لاگ سرور را بررسی کنید.");
            logger.LogWarning(error, "Discovery failed for {TaskId}", candidate.Id);
            await CompleteDiscoveryAsync(candidate.Id, lease, null, detail, ct);
        }
    }

    private IQueryable<DiscoveryTask> DueDiscovery(DateTime now) => db.DiscoveryTasks.Where(x =>
        x.Kind == DiscoveryTaskKind.Discovery && x.BrandId != null && x.Brand != null && !x.Brand.IsDeleted
        && (x.Status == DiscoveryTaskStatus.Queued || (x.Status == DiscoveryTaskStatus.Running
            && (x.LeaseUntil == null || x.LeaseUntil <= now))));

    private async Task CompleteDiscoveryAsync(Guid id, DateTime lease, IReadOnlyList<DiscoveredProduct>? results,
        string? error, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var now = DateTime.UtcNow;
        var acquired = await db.DiscoveryTasks.Where(x => x.Id == id && x.Status == DiscoveryTaskStatus.Running
            && x.LeaseUntil == lease && x.LeaseUntil > now && x.BrandId != null && x.Brand != null && !x.Brand.IsDeleted)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.LeaseUntil, (DateTime?)null), ct);
        if (acquired == 0)
            return;

        var task = await db.DiscoveryTasks.Include(x => x.Brand).SingleAsync(x => x.Id == id, ct);
        var brand = task.Brand!;
        task.FinishedAt = now;
        if (results == null)
        {
            task.Status = DiscoveryTaskStatus.Failed;
            task.Message = error;
            brand.DiscoveryError = error;
            db.Notifications.Add(new Notification
            {
                Kind = NotificationKind.Error, BrandId = brand.Id, Title = $"جست‌وجوی برند ناموفق بود: {error}",
                CreatedAt = now
            });
        }
        else
        {
            await SaveDiscoveredAsync(brand.Id, results, now, ct);
            task.Status = DiscoveryTaskStatus.Done;
            task.Message = $"جست‌وجو کامل شد؛ {results.Count} محصول مرتبط یافت شد.";
            brand.LastDiscoveredAt = now;
            brand.DiscoveryError = null;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task SaveDiscoveredAsync(Guid brandId, IReadOnlyList<DiscoveredProduct> results, DateTime now,
        CancellationToken ct)
    {
        foreach (var batch in results.DistinctBy(x => x.SourceId).Chunk(500))
        {
            var ids = batch.Select(x => x.SourceId).ToArray();
            var existing = await db.Products.IgnoreQueryFilters().Where(x => ids.Contains(x.SourceId))
                .ToDictionaryAsync(x => x.SourceId, ct);
            foreach (var item in batch)
            {
                var url = ProductLinks.Canonical(item.SourceId);
                if (existing.TryGetValue(item.SourceId, out var product))
                {
                    if (!product.IsDeleted && product.BrandId == brandId)
                    {
                        product.Title = item.Title;
                        product.Url = url;
                        product.ImageUrl = item.ImageUrl ?? product.ImageUrl;
                    }
                    continue;
                }
                product = new Product
                {
                    BrandId = brandId, SourceId = item.SourceId, Title = item.Title, Url = url, ImageUrl = item.ImageUrl
                };
                db.Products.Add(product);
                db.Notifications.Add(new Notification
                {
                    Kind = NotificationKind.ProductFound, BrandId = brandId, Product = product,
                    Title = $"محصول جدید برای انتخاب: {item.Title}", CreatedAt = now
                });
            }
        }
    }

    public async Task RunMaintenanceAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var config = await MonitoringSettings.LoadAsync(db, ct);
        var retentionCutoff = now.AddDays(-config.RetentionDays);
        var oldIds = db.PriceObservations.IgnoreQueryFilters().Where(x => x.ObservedAt < retentionCutoff)
            .OrderBy(x => x.ObservedAt).ThenBy(x => x.Id).Select(x => x.Id).Take(10_000);
        await db.PriceObservations.IgnoreQueryFilters().Where(x => oldIds.Contains(x.Id)).ExecuteDeleteAsync(ct);

        var cutoff = now.AddHours(-config.DiscoveryHours);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var due = await db.Brands.Where(b => b.LastDiscoveredAt != null && b.LastDiscoveredAt <= cutoff
            && !db.DiscoveryTasks.Any(t => t.BrandId == b.Id && (t.CreatedAt >= cutoff
                || t.Status == DiscoveryTaskStatus.Queued || t.Status == DiscoveryTaskStatus.Running)))
            .Select(b => b.Id).ToListAsync(ct);
        foreach (var brandId in due)
            db.DiscoveryTasks.Add(new DiscoveryTask { BrandId = brandId });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task RecordHeartbeatAsync(string key, DateTime now, CancellationToken ct)
    {
        var value = now.ToString("O", CultureInfo.InvariantCulture);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var setting = await db.AppSettings.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Key == key, ct);
        if (setting == null)
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        else
        {
            setting.Value = value;
            setting.IsDeleted = false;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    private static string SourceMessage(Exception error, string fallback) => error switch
    {
        ProductSourceException => error.Message,
        OperationCanceledException => "زمان انتظار برای پاسخ اسنپ‌شاپ به پایان رسید.",
        _ => fallback
    };
}
