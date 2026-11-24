using System.Globalization;
using System.Text.Json;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Api.Panel;

internal static class PanelRequest
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    internal static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        var hasZone = value.EndsWith('Z') || (value.Length >= 6
            && value[^6] is '+' or '-' && value[^3] == ':');
        if (hasZone && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var timestamp))
            return timestamp.UtcDateTime;
        throw new PanelException(422, "زمان باید ISO 8601 و دارای منطقهٔ زمانی باشد.");
    }
}
