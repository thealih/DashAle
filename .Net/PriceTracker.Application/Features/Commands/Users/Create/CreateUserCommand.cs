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

public sealed record CreateUserCommand(string? Username, string? Password, string? Role)
    : IRequest<CreatedUser>;

public sealed record CreatedUser(Guid Id, string Username, string Role, bool IsActive);

public sealed class CreateUserCommandHandler(AppDbContext db, IPasswordHasher<User> hasher)
    : IRequestHandler<CreateUserCommand, CreatedUser>
{
    public async Task<CreatedUser> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var username = request.Username?.Trim().ApplyCorrectYeKe();
        if (string.IsNullOrWhiteSpace(username) || username.Length > 100 || username.Any(char.IsControl))
            throw new PanelException(422, "نام کاربری معتبر با حداکثر ۱۰۰ کاراکتر وارد کنید.");
        if (request.Password == null || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length is < 12 or > 500)
            throw new PanelException(422, "رمز عبور باید ۱۲ تا ۵۰۰ کاراکتر داشته باشد.");

        var role = request.Role ?? "viewer";
        if (role is not ("admin" or "viewer"))
            throw new PanelException(422, "نقش کاربر باید admin یا viewer باشد.");

        if (await db.Users.IgnoreQueryFilters().AnyAsync(x => x.UserName == username, ct))
            throw UsernameInUse();

        var user = new User { UserName = username, Role = role == "admin" ? UserRole.Admin : UserRole.Viewer };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
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
