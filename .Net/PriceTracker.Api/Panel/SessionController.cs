using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Infrastracture;

namespace PriceTracker.Api.Panel;

[ApiController, ApiVersionNeutral]
[Route("api")]
public sealed class SessionController(AppDbContext db, IPasswordHasher<User> hasher) : ControllerBase
{
    public sealed record Credentials(string? Username, string? Password);

    [HttpPost("auth/login"), EnableRateLimiting(PanelSecurity.LoginLimit)]
    public async Task<IActionResult> Login(Credentials? input, CancellationToken ct)
    {
        var username = input?.Username?.Trim();
        if (!ModelState.IsValid || username == null || username.Length is < 1 or > 100 ||
            string.IsNullOrEmpty(input?.Password) || input.Password.Length > 500)
            return UnprocessableEntity(new { detail = "نام کاربری و رمز عبور معتبر وارد کنید." });

        var user = await db.Users.SingleOrDefaultAsync(x => x.UserName == username && x.IsActive, ct);
        var valid = user == null || !PanelSecurity.IsKnownRole(user.Role)
            ? PasswordVerificationResult.Failed
            : hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password);
        if (valid == PasswordVerificationResult.Failed || user == null)
            return Unauthorized(new { detail = "نام کاربری یا رمز عبور درست نیست." });

        if (valid == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var upgradedHash = hasher.HashPassword(user, input.Password);
            await db.Users.Where(x => x.Id == user.Id && x.PasswordHash == user.PasswordHash)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.PasswordHash, upgradedHash), ct);
        }

        var role = PanelSecurity.RoleName(user.Role);
        var identity = new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("username", user.UserName),
            new(ClaimTypes.Role, role)
        ], PanelSecurity.Scheme);
        await HttpContext.SignInAsync(PanelSecurity.Scheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
        return Ok(new { username = user.UserName, role });
    }

    [HttpPost("auth/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(PanelSecurity.Scheme);
        return Ok(new { ok = true });
    }

    [HttpGet("me"), Authorize(AuthenticationSchemes = PanelSecurity.Scheme)]
    public IActionResult Me() => Ok(new
    {
        username = User.Identity!.Name, role = User.FindFirstValue(ClaimTypes.Role), mode = "live"
    });

    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
            ? Ok(new { status = "ok", mode = "live" })
            : StatusCode(503, new { detail = "ارتباط با دیتابیس برقرار نیست." });
}
