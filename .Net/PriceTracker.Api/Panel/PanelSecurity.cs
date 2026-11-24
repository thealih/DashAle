using System.Security.Claims;
using MediatR;
using PriceTracker.Application.Features.Commands.Users.Create;
using PriceTracker.Application.Features.Panel;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Api.Panel;

public static class PanelSecurity
{
    public const string Scheme = "PriceTrackerPanel";
    public const string Admin = "PanelAdmin";
    public const string LoginLimit = "PanelLogin";

    public static void AddPanelSecurity(this IServiceCollection services)
    {
        services.AddAuthentication(Scheme).AddCookie(Scheme, options =>
        {
            options.Cookie.Name = "price_tracker_session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(12);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context =>
                Error(context.HttpContext, 401, "برای ادامه وارد حساب کاربری شوید.");
            options.Events.OnRedirectToAccessDenied = context =>
                Error(context.HttpContext, 403, "این عملیات به دسترسی مدیر نیاز دارد.");
            options.Events.OnValidatePrincipal = ValidateSession;
        });
        services.AddAuthorization(options => options.AddPolicy(Admin, policy =>
            policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser().RequireRole("admin")));
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(LoginLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0
                }));
            options.OnRejected = (context, _) => new ValueTask(Error(context.HttpContext, 429,
                "تلاش‌های ورود بیش از حد است؛ پنج دقیقه بعد دوباره امتحان کنید."));
        });
    }

    private static async Task ValidateSession(CookieValidatePrincipalContext context)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        if (Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive,
                context.HttpContext.RequestAborted);
            if (user != null && IsKnownRole(user.Role) &&
                context.Principal!.FindFirstValue(ClaimTypes.Role) == RoleName(user.Role) &&
                context.Principal.FindFirstValue(ClaimTypes.Name) == user.UserName)
                return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(Scheme);
    }

    public static bool IsKnownRole(UserRole role) => role is UserRole.Admin or UserRole.Viewer;
    public static string RoleName(UserRole role) => role == UserRole.Admin ? "admin" : "viewer";

    public static Task Error(HttpContext context, int code, string detail)
    {
        context.Response.StatusCode = code;
        return context.Response.WriteAsJsonAsync(new { detail }, context.RequestAborted);
    }

    public static async Task<int> CreateUserAsync(IServiceProvider services, string username, string role)
    {
        if (!Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Pipe the password through stdin. Do not pass it as a command argument.");
            return 1;
        }
        Console.InputEncoding = System.Text.Encoding.UTF8;
        var password = await Console.In.ReadLineAsync();
        await using var scope = services.CreateAsyncScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new CreateUserCommand(username, password, role));
        }
        catch (PanelException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        Console.WriteLine("Panel user created.");
        return 0;
    }
}
