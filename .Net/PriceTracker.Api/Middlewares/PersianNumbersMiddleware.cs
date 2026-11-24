using DNTPersianUtils.Core;
using System.Net.Mime;
using System.Text;
using System.Web;

namespace PriceTracker.Api.Middlewares
{
    public class PersianNumbersMiddleware
    {
        private readonly RequestDelegate _next;

        public PersianNumbersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;

            #region Route

            var routeValues = request.RouteValues;

            foreach (var key in routeValues.Keys)
                if (routeValues[key] is string value)
                    routeValues[key] = value.ToEnglishNumbers();

            context.Request.RouteValues = routeValues;

            #endregion

            #region Query

            if (!string.IsNullOrWhiteSpace(request.QueryString.ToString()))
            {
                var decodedQueryString = HttpUtility.UrlDecode(request.QueryString.ToString(), Encoding.UTF8);
                var normalizedQueryString = decodedQueryString.ToEnglishNumbers();
                context.Request.QueryString = new(normalizedQueryString);
            }

            #endregion

            #region Body

            if (!string.IsNullOrWhiteSpace(request.ContentType)
                && string.Equals(request.ContentType, MediaTypeNames.Application.Json))
            {
                request.EnableBuffering();
                var stringBody = await new StreamReader(request.Body).ReadToEndAsync();
                var normalizedStringBody = stringBody.ToEnglishNumbers();
                request.Body.Position = 0;
                var bodyBytes = Encoding.UTF8.GetBytes(normalizedStringBody);
                var bodyStream = new MemoryStream(bodyBytes);
                context.Request.Body = bodyStream;
            }

            #endregion

            await _next(context);
        }
    }
}
