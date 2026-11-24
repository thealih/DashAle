using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Panel;

public sealed record OwnStoreView(string SellerId, string SellerName, int UndercutToman, bool Matched);

internal static class OwnStore
{
    internal static readonly string[] Keys = ["own_seller_id", "own_seller_name", "undercut_toman"];

    internal static async Task<OwnStoreView> LoadAsync(AppDbContext db, CancellationToken ct)
    {
        var settings = await db.AppSettings.AsNoTracking().Where(x => Keys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, x => x.Value, ct);
        var sellerId = settings.GetValueOrDefault(Keys[0], "");
        var seller = await FindSeller(db, sellerId, ct);
        var undercut = int.TryParse(settings.GetValueOrDefault(Keys[2]), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var value) && value is >= 1 and <= 1_000_000_000 ? value : 1;
        return new(seller?.SourceId ?? sellerId, seller?.Name ?? settings.GetValueOrDefault(Keys[1], ""),
            undercut, seller != null);
    }

    internal static Task<Seller?> FindSeller(AppDbContext db, string id, CancellationToken ct) =>
        id.Length == 0 ? Task.FromResult<Seller?>(null) :
            db.Sellers.AsNoTracking().SingleOrDefaultAsync(x => x.SourceId == id, ct);
}
