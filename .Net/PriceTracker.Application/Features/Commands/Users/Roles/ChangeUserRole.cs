using System.Data;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Commands.Users.Roles;

public sealed record ChangeUserRole(Guid TargetId, string? Role, Guid ActorId, Guid ActorSessionId)
    : IRequest<CreatedUser>;

public sealed class ChangeUserRoleHandler(AppDbContext db) : IRequestHandler<ChangeUserRole, CreatedUser>
{
    public async Task<CreatedUser> Handle(ChangeUserRole request, CancellationToken ct)
    {
        var role = UserRoleInput.Parse(request.Role);
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

    private static CreatedUser View(User user) =>
        new(user.Id, user.UserName, user.Role == UserRole.Admin ? "admin" : "viewer", user.IsActive);
}
