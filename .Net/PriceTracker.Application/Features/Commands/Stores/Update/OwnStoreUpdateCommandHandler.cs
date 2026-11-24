using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Infrastracture;
using System.Data;
using System.Globalization;

namespace PriceTracker.Application.Features.Commands.Stores.Update;

public sealed class OwnStoreUpdateCommandHandler(AppDbContext db) : IRequestHandler<OwnStoreUpdateCommand, OwnStoreView>
{
    public async Task<OwnStoreView> Handle(OwnStoreUpdateCommand request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var seller = await OwnStore.FindSeller(db, request.SellerId, ct);
        var sellerId = seller?.SourceId ?? request.SellerId;
        var name = seller?.Name ?? request.SellerName.Trim();
        if (sellerId.Length > 0 && name.Length == 0)
            throw new PanelException(422, "برای شناسه‌ای که هنوز شناسایی نشده، نام فروشگاه را هم وارد کنید.");
        if (sellerId.Length == 0)
            name = "";

        string[] values = [sellerId, name, request.UndercutToman.ToString(CultureInfo.InvariantCulture)];
        var settings = await db.AppSettings.IgnoreQueryFilters().Where(x => OwnStore.Keys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, ct);
        for (var i = 0; i < OwnStore.Keys.Length; i++)
        {
            if (settings.TryGetValue(OwnStore.Keys[i], out var row))
            {
                row.Value = values[i];
                row.IsDeleted = false;
            }
            else
                db.AppSettings.Add(new AppSetting { Key = OwnStore.Keys[i], Value = values[i] });
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(sellerId, name, request.UndercutToman, seller != null);
    }
}
