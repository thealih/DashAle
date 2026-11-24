using System.Security.Claims;
using System.Data;
using Microsoft.Data.SqlClient;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Infrastracture;

namespace PriceTracker.Api.Panel;

[ApiController, ApiVersionNeutral]
[Route("api")]
public sealed class SessionController(AppDbContext db, IPasswordHasher<User> hasher, PanelJwt jwt, IWebHostEnvironment environment) : ControllerBase
{
    public sealed record Credentials(string? Username, string? Password);
    public sealed record RefreshBody(
        [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken);

    [HttpPost("auth/login"), EnableRateLimiting(PanelSecurity.LoginLimit)]
    public async Task<IActionResult> Login(Credentials? input, CancellationToken ct)
    {
        var username = input?.Username?.Trim();
        if (!ModelState.IsValid || username == null || username.Length is < 1 or > 100 ||
            string.IsNullOrEmpty(input?.Password) || input.Password.Length > 500)
            return UnprocessableEntity(new { detail = "نام کاربری و رمز عبور معتبر وارد کنید." });

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserName == username && x.IsActive, ct);
        var valid = user == null || !PanelSecurity.IsKnownRole(user.Role)
            ? PasswordVerificationResult.Failed
            : hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password);
        if (valid == PasswordVerificationResult.Failed || user == null)
            return Unauthorized(new { detail = "نام کاربری یا رمز عبور درست نیست." });

        var now = DateTime.UtcNow;
        var session = new UserSession
        {
            Id = Guid.NewGuid(), UserId = user.Id, ExpiresAt = now.AddDays(jwt.RefreshLifetimeDays)
        };
        var refreshToken = PanelJwt.NewRefreshToken();

        // Keep session creation and account role updates ordered by the database.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var current = await db.Users.SingleOrDefaultAsync(x => x.Id == user.Id && x.IsActive, ct);
            if (current == null || current.Role != user.Role || current.PasswordHash != user.PasswordHash
                || current.UserName != user.UserName)
                return Unauthorized(new { detail = "اطلاعات حساب تغییر کرده است؛ دوباره وارد شوید." });

            if (valid == PasswordVerificationResult.SuccessRehashNeeded)
                current.PasswordHash = hasher.HashPassword(current, input.Password);

            await db.UserSessions.IgnoreQueryFilters().Where(x => x.UserId == user.Id && x.ExpiresAt <= now)
                .ExecuteDeleteAsync(ct);
            db.UserSessions.Add(session);
            StoreRefresh(session.Id, refreshToken);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when ((ex as SqlException ?? ex.InnerException as SqlException) is { Number: 1205 })
        {
            return Conflict(new { detail = "اطلاعات حساب هم‌زمان تغییر کرد؛ دوباره تلاش کنید." });
        }

        return TokenResponse(user, session, refreshToken, now);
    }

    [HttpPost("auth/refresh")]
    public async Task<IActionResult> Refresh([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshBody? input,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return UnprocessableEntity(new { detail = "بدنهٔ درخواست معتبر نیست." });
        var hash = ReadRefreshHash(input);
        if (hash == null)
            return RefreshRejected();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // Serialize use of a refresh credential before checking whether it was consumed.
            var hashParameter = new SqlParameter("@hash", SqlDbType.Char, 64) { Value = hash };
            var token = await db.UserRefreshTokens
                .FromSqlInterpolated($"SELECT * FROM [UserRefreshTokens] WITH (UPDLOCK, HOLDLOCK) WHERE [Hash] = {hashParameter}")
                .Include(x => x.Session).ThenInclude(x => x.User)
                .SingleOrDefaultAsync(ct);
            var now = DateTime.UtcNow;
            if (token == null || token.Session.ExpiresAt <= now || !token.Session.User.IsActive
                || !PanelSecurity.IsKnownRole(token.Session.User.Role))
                return RefreshRejected();

            if (token.UsedAt != null)
            {
                // A consumed credential was presented again. Revoke the whole session.
                await db.UserSessions.IgnoreQueryFilters().Where(x => x.Id == token.SessionId).ExecuteDeleteAsync(ct);
                await transaction.CommitAsync(ct);
                ClearCookies();
                return RefreshRejected();
            }

            token.UsedAt = now;
            var replacement = PanelJwt.NewRefreshToken();
            StoreRefresh(token.SessionId, replacement);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return TokenResponse(token.Session.User, token.Session, replacement, now);
        }
        catch (Exception ex) when ((ex as SqlException ?? ex.InnerException as SqlException) is { Number: 1205 })
        {
            return Conflict(new { detail = "نشست هم‌زمان در حال تمدید است؛ دوباره تلاش کنید." });
        }
    }

    [HttpPost("auth/logout")]
    public async Task<IActionResult> Logout([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshBody? input,
        CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated == true && PanelSecurity.TrySessionIds(User, out var userId, out var sessionId))
            await db.UserSessions.IgnoreQueryFilters().Where(x => x.Id == sessionId && x.UserId == userId)
                .ExecuteDeleteAsync(ct);
        else if (ReadRefreshHash(input) is string hash)
            await db.UserSessions.IgnoreQueryFilters()
                .Where(session => db.UserRefreshTokens.IgnoreQueryFilters().Any(x => x.SessionId == session.Id && x.Hash == hash))
                .ExecuteDeleteAsync(ct);

        ClearCookies();
        return Ok(new { ok = true });
    }

    private string? ReadRefreshHash(RefreshBody? input) =>
        PanelJwt.RefreshHash(input?.RefreshToken ?? Request.Cookies[PanelJwt.RefreshCookieName]);

    private void StoreRefresh(Guid sessionId, string token) =>
        db.UserRefreshTokens.Add(new UserRefreshToken { SessionId = sessionId, Hash = PanelJwt.RefreshHash(token)! });

    private IActionResult TokenResponse(User user, UserSession session, string refreshToken, DateTime now)
    {
        var accessExpiry = now.AddMinutes(jwt.LifetimeMinutes);
        if (accessExpiry > session.ExpiresAt)
            accessExpiry = session.ExpiresAt;
        var accessToken = jwt.Issue(user, session, now, accessExpiry);
        var accessCookie = SessionCookie("/");
        accessCookie.Expires = accessExpiry;
        var refreshCookie = SessionCookie("/api/auth");
        refreshCookie.Expires = session.ExpiresAt;
        Response.Cookies.Append(PanelJwt.CookieName, accessToken, accessCookie);
        Response.Cookies.Append(PanelJwt.RefreshCookieName, refreshToken, refreshCookie);
        return Ok(new
        {
            username = user.UserName, role = PanelSecurity.RoleName(user.Role),
            access_token = accessToken, token_type = "Bearer",
            expires_in = (int)(accessExpiry - now).TotalSeconds,
            refresh_token = refreshToken, refresh_expires_at = session.ExpiresAt
        });
    }

    private IActionResult RefreshRejected() => Unauthorized(new { detail = "نشست دیگر معتبر نیست؛ دوباره وارد شوید." });

    private void ClearCookies()
    {
        Response.Cookies.Delete(PanelJwt.CookieName, SessionCookie("/"));
        Response.Cookies.Delete(PanelJwt.RefreshCookieName, SessionCookie("/api/auth"));
    }

    private CookieOptions SessionCookie(string path) => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = !environment.IsDevelopment() || Request.IsHttps,
        Path = path
    };

    [HttpGet("me"), Authorize(AuthenticationSchemes = PanelSecurity.Scheme)]
    public IActionResult Me() => Ok(new
    {
        username = User.Identity!.Name, role = User.FindFirstValue(PanelJwt.RoleClaim), mode = "live"
    });

    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
            ? Ok(new { status = "ok", mode = "live" })
            : StatusCode(503, new { detail = "ارتباط با دیتابیس برقرار نیست." });
}
