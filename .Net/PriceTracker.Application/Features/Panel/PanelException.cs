namespace PriceTracker.Application.Features.Panel;

public sealed class PanelException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
