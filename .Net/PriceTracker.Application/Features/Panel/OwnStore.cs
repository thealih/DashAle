using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Panel;

public sealed record OwnStoreView(string SellerId, string SellerName, int UndercutToman, bool Matched);
public sealed record GetOwnStore : IRequest<OwnStoreView>;
public sealed record SetOwnStore(string SellerId, string SellerName, int UndercutToman) : IRequest<OwnStoreView>;
public sealed record SellerList : IRequest<object>;

public sealed partial class OwnStore(AppDbContext db) :
    IRequestHandler<GetOwnStore, OwnStoreView>, IRequestHandler<SetOwnStore, OwnStoreView>,
    IRequestHandler<SellerList, object>
{
    private static readonly string[] Keys = ["own_seller_id", "own_seller_name", "undercut_toman"];

    public Task<OwnStoreView> Handle(GetOwnStore request, CancellationToken ct) => LoadAsync(db, ct);

    public async Task<object> Handle(SellerList request, CancellationToken ct) =>
        await db.Sellers.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.SourceId)
            .Select(x => new { id = x.SourceId, x.Name }).ToListAsync(ct);

    public async Task<OwnStoreView> Handle(SetOwnStore request, CancellationToken ct)
    {
        if (request.SellerId.Length > 100 || !SellerIdPattern().IsMatch(request.SellerId) ||
            request.SellerName.Length > 300 || request.UndercutToman is < 1 or > 1_000_000_000)
            throw new PanelException(422, "تنظیمات فروشگاه پیش‌فرض معتبر نیست.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var seller = await FindSeller(db, request.SellerId, ct);
        var sellerId = seller?.SourceId ?? request.SellerId;
        var name = seller?.Name ?? request.SellerName.Trim();
        if (sellerId.Length > 0 && name.Length == 0)
            throw new PanelException(422, "برای شناسه‌ای که هنوز شناسایی نشده، نام فروشگاه را هم وارد کنید.");
        if (sellerId.Length == 0)
            name = "";

        string[] values = [sellerId, name, request.UndercutToman.ToString(CultureInfo.InvariantCulture)];
        var settings = await db.AppSettings.IgnoreQueryFilters().Where(x => Keys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, ct);
        for (var i = 0; i < Keys.Length; i++)
        {
            if (settings.TryGetValue(Keys[i], out var row))
            {
                row.Value = values[i];
                row.IsDeleted = false;
            }
            else
                db.AppSettings.Add(new AppSetting { Key = Keys[i], Value = values[i] });
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(sellerId, name, request.UndercutToman, seller != null);
    }

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

    private static Task<Seller?> FindSeller(AppDbContext db, string id, CancellationToken ct) =>
        id.Length == 0 ? Task.FromResult<Seller?>(null) :
            db.Sellers.AsNoTracking().SingleOrDefaultAsync(x => x.SourceId == id, ct);

    [GeneratedRegex(@"\A[A-Za-z0-9_-]*\z")]
    private static partial Regex SellerIdPattern();
}
