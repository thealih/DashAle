using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PriceTracker.Domain.IServices;

namespace PriceTracker.Infrastracture.Services;

public sealed partial class SnappShopProductSource(HttpClient client) : IProductSource, IBrandProductSource
{
    private readonly SemaphoreSlim _requestGate = new(1);
    private readonly string _session = Guid.NewGuid().ToString();
    private string? _token;
    private long _lastRequest;

    public async Task<SourceProduct> GetProductAsync(string sourceId, CancellationToken cancellationToken)
    {
        if (!Regex.IsMatch(sourceId, "^[0-9]{3,30}$"))
            throw new ProductSourceException("شناسهٔ محصول معتبر نیست.");
        using var product = await FetchAsync($"products/v2/{sourceId}", null, cancellationToken);
        return ParseProduct(product.RootElement);
    }

    public async Task<IReadOnlyList<DiscoveredProduct>> DiscoverAsync(string aliases, CancellationToken ct)
    {
        var queries = aliases.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var keywords = queries.Select(Normalize).Where(x => x.Length != 0).Distinct().ToArray();
        var products = new Dictionary<string, DiscoveredProduct>(StringComparer.Ordinal);
        foreach (var query in queries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var seenPages = new HashSet<string>(StringComparer.Ordinal);
            for (var page = 1; ; page++)
            {
                var request = new Dictionary<string, object> { ["render"] = 4, ["query"] = query };
                if (page > 1)
                    request["skip"] = page - 1;
                using var response = await FetchAsync("search/v1", request, ct);
                var section = Items(Property(Property(response.RootElement, "data"), "structure"))
                    .FirstOrDefault(x => Text(Property(x, "section_type")) == "plp");
                var items = Property(section, "items");
                if (items.ValueKind != JsonValueKind.Array)
                    throw new ProductSourceException("ساختار نتایج جست‌وجوی اسنپ‌شاپ قابل تشخیص نیست.");
                if (items.GetArrayLength() == 0)
                    break;

                var signature = string.Join("\n", items.EnumerateArray().Select(x => Text(Property(x, "href"))));
                if (!seenPages.Add(signature))
                    throw new ProductSourceException("صفحه‌بندی جست‌وجو تکراری بود؛ نتیجهٔ کامل تأیید نشد.");

                foreach (var item in items.EnumerateArray())
                {
                    var title = Text(Property(item, "title"));
                    if (string.IsNullOrWhiteSpace(title) || title.Length > 600)
                        continue;
                    var normalized = Normalize(title);
                    if (!keywords.Any(x => normalized.Contains(x, StringComparison.Ordinal)))
                        continue;
                    var match = ProductHref().Match(Text(Property(item, "href")) ?? "");
                    if (!match.Success)
                        continue;
                    var image = Property(item, "image");
                    if (!Truthy(image))
                        image = Property(item, "images");
                    if (image.ValueKind == JsonValueKind.Array)
                        image = Items(image).FirstOrDefault();
                    if (image.ValueKind == JsonValueKind.Object)
                        image = Property(image, "src").ValueKind == JsonValueKind.String
                            ? Property(image, "src") : Property(image, "url");
                    var imageUrl = Text(image);
                    products[match.Groups[1].Value] = new(match.Groups[1].Value, title,
                        Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https" ? imageUrl : null);
                }
                var total = Property(Property(section, "pagination"), "total_pages");
                if (total.ValueKind != JsonValueKind.Number || !total.TryGetInt32(out var pages) || pages < 0)
                    throw new ProductSourceException("اطلاعات صفحه‌بندی جست‌وجو دریافت نشد.");
                if (page >= pages)
                    break;
                if (page >= 500)
                    throw new ProductSourceException("سقف صفحات جست‌وجو رسید؛ نتیجهٔ کامل تأیید نشد.");
            }
        }
        return products.Values.ToArray();
    }

    private async Task<JsonDocument> FetchAsync(string path, object? body, CancellationToken cancellationToken)
    {
        await _requestGate.WaitAsync(cancellationToken);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            var ct = timeout.Token;
            try
            {
                _token ??= await GetGuestTokenAsync(ct);
                return await GetJsonAsync(path, body, _token, ct);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                _token = null;
                _token = await GetGuestTokenAsync(ct);
                return await GetJsonAsync(path, body, _token, ct);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProductSourceException("دریافت اطلاعات از اسنپ‌شاپ بیش از حد طول کشید.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode status)
        {
            throw new ProductSourceException($"دریافت اطلاعات از اسنپ‌شاپ ناموفق بود (HTTP {(int)status}).");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            throw new ProductSourceException("اتصال به اسنپ‌شاپ برقرار نشد.");
        }
        catch (JsonException)
        {
            throw new ProductSourceException("پاسخ اسنپ‌شاپ JSON معتبر نیست.");
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private async Task<string> GetGuestTokenAsync(CancellationToken ct)
    {
        using var guest = await GetJsonAsync("guest/v2/token", null, null, ct);
        var token = Text(Property(Property(guest.RootElement, "data"), "token"));
        return !string.IsNullOrEmpty(token) ? token
            : throw new ProductSourceException("توکن بازدیدکننده از اسنپ‌شاپ دریافت نشد.");
    }

    private async Task<JsonDocument> GetJsonAsync(string path, object? body, string? token, CancellationToken ct)
    {
        if (_lastRequest != 0)
        {
            var delay = TimeSpan.FromSeconds(1) - System.Diagnostics.Stopwatch.GetElapsedTime(_lastRequest);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);
        }
        _lastRequest = System.Diagnostics.Stopwatch.GetTimestamp();
        using var request = new HttpRequestMessage(body == null ? HttpMethod.Get : HttpMethod.Post, path + "?lat=35.77331&lng=51.418591");
        if (body != null)
            request.Content = JsonContent.Create(body);
        request.Headers.TryAddWithoutValidation("Origin", "https://snappshop.ir");
        request.Headers.Referrer = new Uri("https://snappshop.ir/");
        request.Headers.TryAddWithoutValidation("Accept-Language", "fa-IR,fa;q=0.9");
        request.Headers.TryAddWithoutValidation("x-origin", "https://snappshop.ir");
        request.Headers.TryAddWithoutValidation("s-device", "DESKTOP");
        request.Headers.TryAddWithoutValidation("s-device-source", "shop");
        request.Headers.TryAddWithoutValidation("s-area-ids", "29,197");
        request.Headers.TryAddWithoutValidation("s-session-id", _session);
        request.Headers.TryAddWithoutValidation("uuid", _session);
        if (token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException("Unexpected source HTTP response.", null, response.StatusCode);
        await using var responseBody = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(responseBody, cancellationToken: ct);
    }

    public static SourceProduct ParseProduct(JsonElement response)
    {
        var data = Property(response, "data");
        var title = Text(Property(Property(data, "content"), "title_fa"));
        var variants = Property(data, "variants");
        var vendors = Property(data, "vendors");
        if (string.IsNullOrWhiteSpace(title) || title.Length > 600 || variants.ValueKind != JsonValueKind.Array
            || vendors.ValueKind != JsonValueKind.Array || Truthy(Property(data, "next"))
            || Truthy(Property(Property(data, "pagination"), "next"))
            || (variants.GetArrayLength() == 0 && vendors.GetArrayLength() != 0))
            throw InvalidResponse();

        var sellers = Map(vendors, x => Text(Property(x, "id")), x => Text(Property(x, "title")));
        var attributes = Map(Property(data, "configurable_attribute"),
            x => Text(Property(Property(x, "value"), "id")),
            x => $"{Text(Property(x, "title"))}: {Text(Property(Property(x, "value"), "title"))}");
        var warranties = Map(Property(data, "warranties"), x => Text(Property(x, "id")), x => Text(Property(x, "name")));
        var offers = new List<SourceOffer>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variant in variants.EnumerateArray())
        {
            var variantVendors = Property(variant, "vendor");
            if (variantVendors.ValueKind != JsonValueKind.Array)
                throw InvalidResponse();
            var expected = Property(variant, "active_vendors");
            if (expected.ValueKind == JsonValueKind.Number && expected.TryGetInt32(out var count) &&
                count > variantVendors.EnumerateArray().Select(x => Text(Property(x, "vendor_id"))).Distinct().Count())
                throw InvalidResponse();

            var attrs = Items(Property(variant, "attribute_ids")).Select(x => Text(Property(x, "attribute_value_id")))
                .Where(x => x != null).Select(x => x!).Order(StringComparer.Ordinal).ToArray();
            var variantName = attrs.Length == 0 ? "پیش‌فرض" :
                string.Join("، ", attrs.Select(x => attributes.GetValueOrDefault(x) ?? x));
            var identity = attrs.Length == 0
                ? JsonSerializer.Serialize(Text(Property(variant, "variation_id")) ?? Text(Property(variant, "id")) ?? "default")
                : "[" + string.Join(", ", attrs.Select(x => JsonSerializer.Serialize(x))) + "]";
            foreach (var vendor in variantVendors.EnumerateArray())
            {
                var seller = Text(Property(vendor, "vendor_id"));
                if (string.IsNullOrEmpty(seller) || seller.Length > 100 || !sellers.TryGetValue(seller, out var name)
                    || string.IsNullOrWhiteSpace(name) || name.Length > 300)
                    throw InvalidResponse();
                var warrantyId = Text(Property(vendor, "warranty_id")) ?? "";
                var warranty = warranties.GetValueOrDefault(warrantyId);
                var display = string.IsNullOrEmpty(warranty) ? variantName : $"{variantName} · {warranty}";
                var listPrice = Price(Property(vendor, "price"));
                var special = Price(Property(vendor, "special_price"));
                var price = special is > 0 ? special : listPrice;
                var stock = Stock(Property(vendor, "stock"));
                if (display.Length > 500 || price == 0 || (price == null && stock != false))
                    throw InvalidResponse();

                // Preserve the existing Python identity layout so imported offers keep their keys.
                var structuralKey = $"{identity}, {JsonSerializer.Serialize(warrantyId)}";
                var key = Hash($"[{JsonSerializer.Serialize(seller)}, {structuralKey}]");
                if (!keys.Add(key))
                    throw InvalidResponse();
                offers.Add(new(seller, name, key, Hash($"[{structuralKey}]"), display,
                    price, listPrice is > 0 ? listPrice : null, stock));
            }
        }

        var sourceBrand = Property(data, "brand");
        var image = Text(Property(Items(Property(data, "images")).FirstOrDefault(), "src"));
        if (!Uri.TryCreate(image, UriKind.Absolute, out var imageUri) || imageUri.Scheme != "https")
            image = null;
        var outOfStock = offers.Count == 0 && Items(Property(Property(data, "page"), "extra_meta")).Any(x =>
            Text(Property(x, "property")) == "product:availability" && Text(Property(x, "content")) == "out of stock");
        return new(title, image, $"{Text(Property(sourceBrand, "title_fa"))} {Text(Property(sourceBrand, "title_en"))}",
            outOfStock ? "out_of_stock" : "unknown", offers);
    }

    private static string Normalize(string text) => Separators().Replace(
        text.ToLowerInvariant().Replace('ي', 'ی').Replace('ك', 'ک').Replace('آ', 'ا'), "");

    [GeneratedRegex(@"[\s\u200c\u200fـ_\-]+")]
    private static partial Regex Separators();

    [GeneratedRegex(@"/product/(?:snp-)?([0-9]{3,30})(?:[/?#]|$)")]
    private static partial Regex ProductHref();

    private static JsonElement Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    private static IEnumerable<JsonElement> Items(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];

    private static string? Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        _ => null
    };

    private static Dictionary<string, string?> Map(JsonElement array, Func<JsonElement, string?> key,
        Func<JsonElement, string?> value)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var item in Items(array))
        {
            if (key(item) is string id)
                result[id] = value(item);
        }
        return result;
    }

    private static bool Truthy(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.False => false,
        JsonValueKind.String => !string.IsNullOrEmpty(value.GetString()),
        JsonValueKind.Number => value.GetDouble() != 0,
        JsonValueKind.Array => value.GetArrayLength() != 0,
        _ => true
    };

    private static long? Price(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var price)
            && price >= 0 && price <= long.MaxValue && price == decimal.Truncate(price))
            return (long)price;
        throw InvalidResponse();
    }

    private static bool? Stock(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var stock) && stock >= 0)
            return stock > 0;
        throw InvalidResponse();
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static ProductSourceException InvalidResponse() => new("پاسخ محصول یا فهرست فروشندگان اسنپ‌شاپ ناقص یا نامعتبر است.");
}
