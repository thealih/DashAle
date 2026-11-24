using PriceTracker.Api.Middlewares;

namespace PriceTracker.Api.Extensions
{
    public static class MiddlewareExtension
    {
        public static IApplicationBuilder UsePersianNumbersMiddleware(
            this IApplicationBuilder app) => app.UseMiddleware<PersianNumbersMiddleware>();
    }
}
