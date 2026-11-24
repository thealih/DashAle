namespace PriceTracker.Application.Features.Panel;

internal static class ReportRows
{
    internal static PanelException TooMany(int limit) =>
        new(413, $"گزارش بیش از {limit:N0} ردیف است. بازه یا فیلتر را محدود کنید.");
}
