using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Commands.Brands.QueueDiscovery;

public sealed class BrandQueueDiscoveryCommandHandler(AppDbContext db) : IRequestHandler<BrandQueueDiscoveryCommand, Guid>
{
    public Task<Guid> Handle(BrandQueueDiscoveryCommand request, CancellationToken ct) => PanelWriteTransaction.RunAsync(db, async () =>
    {
        var brandId = await db.Brands.Where(x => x.Slug == request.BrandId).Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(ct) ?? throw MissingBrand();
        var existing = await db.DiscoveryTasks
            .Where(x => x.BrandId == brandId && (x.Status == DiscoveryTaskStatus.Queued || x.Status == DiscoveryTaskStatus.Running))
            .OrderBy(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (existing is Guid id)
            return id;
        var task = new DiscoveryTask { BrandId = brandId };
        db.DiscoveryTasks.Add(task);
        await db.SaveChangesAsync(ct);
        return task.Id;
    }, ct);

    private static PanelException MissingBrand() => new(404, "برند پیدا نشد.");
}
