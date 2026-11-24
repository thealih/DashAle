using System.Data;
using DNTPersianUtils.Core;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Commands.Users.Create;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Panel;

public sealed record SearchUsers(string? Search, string? Role, bool? Active, int Page, int PageSize)
    : IRequest<UserSearchResult>;

public sealed record UserSearchResult(IReadOnlyList<CreatedUser> Items, int Total, int Page, int PageSize);

public sealed record ChangeUserRole(Guid TargetId, string? Role, Guid ActorId, Guid ActorSessionId)
    : IRequest<CreatedUser>;

public sealed class UserManagement(AppDbContext db) :
    IRequestHandler<SearchUsers, UserSearchResult>, IRequestHandler<ChangeUserRole, CreatedUser>
{
    public async Task<UserSearchResult> Handle(SearchUsers request, CancellationToken ct)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100 ||
            (long)(request.Page - 1) * request.PageSize > int.MaxValue ||
            request.Search is { Length: > 100 })
            throw new PanelException(422, "عبارت جست‌وجو یا اطلاعات صفحه معتبر نیست.");

        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var text = request.Search.Trim().ApplyCorrectYeKe();
            query = query.Where(x => x.UserName.Contains(text));
        }
        if (request.Role != null)
        {
            var role = ParseRole(request.Role);
            query = query.Where(x => x.Role == role);
        }
        if (request.Active is bool active)
            query = query.Where(x => x.IsActive == active);

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.UserName).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new CreatedUser(x.Id, x.UserName, x.Role == UserRole.Admin ? "admin" : "viewer", x.IsActive))
            .ToListAsync(ct);
        return new UserSearchResult(items, total, request.Page, request.PageSize);
    }

    public async Task<CreatedUser> Handle(ChangeUserRole request, CancellationToken ct)
    {
        var role = ParseRole(request.Role);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var now = DateTime.UtcNow;
            var canManage = await db.UserSessions.AnyAsync(x => x.Id == request.ActorSessionId
                && x.UserId == request.ActorId && x.ExpiresAt > now
                && x.User.IsActive && x.User.Role == UserRole.Admin, ct);
            if (!canManage)
                throw new PanelException(403, "این عملیات به دسترسی مدیر نیاز دارد.");

            var target = await db.Users.SingleOrDefaultAsync(x => x.Id == request.TargetId, ct)
                ?? throw new PanelException(404, "کاربر پیدا نشد.");

            if (target.Role != role)
            {
                if (target.IsActive && target.Role == UserRole.Admin &&
                    !await db.Users.AnyAsync(x => x.Id != target.Id && x.IsActive && x.Role == UserRole.Admin, ct))
                    throw new PanelException(409, "نقش آخرین مدیر فعال سامانه قابل تغییر نیست.");

                target.Role = role;
                await db.SaveChangesAsync(ct);
                await db.UserSessions.IgnoreQueryFilters().Where(x => x.UserId == target.Id).ExecuteDeleteAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return View(target);
        }
        catch (Exception ex) when ((ex as SqlException ?? ex.InnerException as SqlException) is { Number: 1205 })
        {
            throw new PanelException(409, "نقش کاربران هم‌زمان در حال تغییر است؛ دوباره تلاش کنید.");
        }
    }

    private static UserRole ParseRole(string? input) => input switch
    {
        "admin" => UserRole.Admin,
        "viewer" => UserRole.Viewer,
        _ => throw new PanelException(422, "نقش کاربر باید admin یا viewer باشد.")
    };

    private static CreatedUser View(User user) =>
        new(user.Id, user.UserName, user.Role == UserRole.Admin ? "admin" : "viewer", user.IsActive);
}
