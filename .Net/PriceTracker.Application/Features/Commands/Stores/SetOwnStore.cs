using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Commands.Stores;

public sealed record SetOwnStore(string SellerId, string SellerName, int UndercutToman) : IRequest<OwnStoreView>;

public sealed partial class SetOwnStoreHandler(AppDbContext db) : IRequestHandler<SetOwnStore, OwnStoreView>
{
    public async Task<OwnStoreView> Handle(SetOwnStore request, CancellationToken ct)
    {
        if (request.SellerId.Length > 100 || !SellerIdPattern().IsMatch(request.SellerId) ||
            request.SellerName.Length > 300 || request.UndercutToman is < 1 or > 1_000_000_000)
            throw new PanelException(422, "تنظیمات فروشگاه پیش‌فرض معتبر نیست.");

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

    [GeneratedRegex(@"\A[A-Za-z0-9_-]*\z")]
    private static partial Regex SellerIdPattern();
}
