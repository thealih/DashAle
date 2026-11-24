using System.Text.RegularExpressions;

namespace PriceTracker.Application.Features.Panel;

public static partial class ProductLinks
{
    internal const string InvalidUrlMessage = "لینک معتبر صفحهٔ محصول اسنپ‌شاپ را وارد کنید.";

    public static string SourceId(string? url) =>
        TryGetSourceId(url, out var sourceId) ? sourceId : throw new PanelException(422, InvalidUrlMessage);

    internal static bool TryGetSourceId(string? url, out string sourceId)
    {
        sourceId = "";
        if (url is { Length: <= 2000 } && Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && parsed.Scheme == Uri.UriSchemeHttps && parsed.IsDefaultPort && parsed.UserInfo.Length == 0
            && parsed.Host is "snappshop.ir" or "www.snappshop.ir")
        {
            var match = ProductPath().Match(parsed.AbsolutePath);
            if (match.Success)
            {
                sourceId = match.Groups[1].Value;
                return true;
            }
        }
        return false;
    }

    public static string Canonical(string sourceId, string? fallback = null) =>
        NumericId().IsMatch(sourceId) ? $"https://snappshop.ir/product/snp-{sourceId}" : fallback ?? "";

    [GeneratedRegex(@"^/product/(?:snp-)?([0-9]{3,30})(?:/|$)")]
    private static partial Regex ProductPath();

    [GeneratedRegex(@"^[0-9]{3,30}$")]
    private static partial Regex NumericId();
}
