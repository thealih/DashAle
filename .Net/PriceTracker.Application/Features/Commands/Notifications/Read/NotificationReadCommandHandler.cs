using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Commands.Notifications.Read;

public sealed class NotificationReadCommandHandler(AppDbContext db) : IRequestHandler<NotificationReadCommand>
{
    public async Task Handle(NotificationReadCommand request, CancellationToken ct)
    {
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
