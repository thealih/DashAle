using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Commands.Notifications;

public sealed record ReadNotices(long[]? Ids, string? Actor) : IRequest;

public sealed class ReadNoticesHandler(AppDbContext db) : IRequestHandler<ReadNotices>
{
    public async Task Handle(ReadNotices request, CancellationToken ct)
    {
        if (request.Ids is { Length: > 1000 })
            throw new PanelException(422, "حداکثر ۱۰۰۰ اعلان را هم‌زمان انتخاب کنید.");

        var query = db.Notifications.Where(x => !x.IsRead);
        if (request.Ids is { } ids)
        {
            if (ids.Length == 0)
                return;
            query = query.Where(x => ids.Contains(x.PublicId));
        }
        var now = DateTime.UtcNow;
        await query.ExecuteUpdateAsync(set => set.SetProperty(x => x.IsRead, true)
            .SetProperty(x => x.ModifiedAt, now).SetProperty(x => x.ModifiedBy, request.Actor), ct);
    }
}
