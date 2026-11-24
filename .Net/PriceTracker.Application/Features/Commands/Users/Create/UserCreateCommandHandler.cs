using DNTPersianUtils.Core;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Commands.Users.Create;

public sealed class UserCreateCommandHandler(AppDbContext db, IPasswordHasher<User> hasher) : IRequestHandler<UserCreateCommand, CreatedUser>
{
    public async Task<CreatedUser> Handle(UserCreateCommand request, CancellationToken ct)
    {
        var username = request.Username!.Trim().ApplyCorrectYeKe();
        var role = request.Role ?? "viewer";
        if (await db.Users.IgnoreQueryFilters().AnyAsync(x => x.UserName == username, ct))
            throw UsernameInUse();

        var user = new User { UserName = username, Role = role == "admin" ? UserRole.Admin : UserRole.Viewer };
        user.PasswordHash = hasher.HashPassword(user, request.Password!);
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw UsernameInUse();
        }
        return new CreatedUser(user.Id, user.UserName, role, user.IsActive);
    }

    private static PanelException UsernameInUse() => new(409, "این نام کاربری قبلاً ثبت شده است.");
}
