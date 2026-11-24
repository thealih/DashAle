namespace PriceTracker.Application.Features.Panel;

public sealed class PanelException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    internal static PanelException MissingProduct() => new(404, "محصول پیدا نشد یا حذف شده است.");
}
