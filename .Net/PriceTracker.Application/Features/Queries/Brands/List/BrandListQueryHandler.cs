using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Queries.Brands.List;

public sealed class BrandListQueryHandler(AppDbContext db) : IRequestHandler<BrandListQuery, object>
{
    public async Task<object> Handle(BrandListQuery request, CancellationToken ct)
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
}
