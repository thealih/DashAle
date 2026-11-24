using System.Security.Claims;
using MediatR;
using PriceTracker.Application.Features.Commands.Users.Create;
using PriceTracker.Application.Features.Panel;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Api.Panel;

public static class PanelSecurity
{
    public const string Scheme = JwtBearerDefaults.AuthenticationScheme;
    public const string Admin = "PanelAdmin";
    public const string LoginLimit = "PanelLogin";

    public static void AddPanelSecurity(this IServiceCollection services, IConfiguration configuration, bool development)
    {
        var jwt = new PanelJwt(configuration, development);
        services.AddSingleton(jwt);
        services.AddAuthentication(Scheme).AddJwtBearer(Scheme, options =>
        {
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = jwt.ValidationParameters;
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // An explicit Authorization header always takes precedence over the browser cookie.
                    if (!context.Request.Headers.ContainsKey("Authorization"))
                        context.Token = context.Request.Cookies[PanelJwt.CookieName];
                    return Task.CompletedTask;
                },
                OnTokenValidated = ValidateSession,
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                    return Error(context.HttpContext, 401, "برای ادامه وارد حساب کاربری شوید.");
                },
                OnForbidden = context => Error(context.HttpContext, 403, "این عملیات به دسترسی مدیر نیاز دارد.")
            };
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

    private static async Task ValidateSession(TokenValidatedContext context)
    {
        var principal = context.Principal!;
        if (TrySessionIds(principal, out var userId, out var sessionId))
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            var user = await db.UserSessions.AsNoTracking()
                .Where(x => x.Id == sessionId && x.UserId == userId && x.ExpiresAt > now && x.User.IsActive)
                .Select(x => new { x.User.UserName, x.User.Role })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            if (user != null && IsKnownRole(user.Role) &&
                principal.FindFirstValue(PanelJwt.RoleClaim) == RoleName(user.Role) &&
                principal.FindFirstValue(PanelJwt.NameClaim) == user.UserName)
                return;
        }

        context.Fail("The session is no longer valid.");
    }

    public static bool TrySessionIds(ClaimsPrincipal principal, out Guid userId, out Guid sessionId)
    {
        var validUser = Guid.TryParse(principal.FindFirstValue(PanelJwt.SubjectClaim), out userId);
        var validSession = Guid.TryParse(principal.FindFirstValue(PanelJwt.SessionClaim), out sessionId);
        return validUser && validSession;
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
