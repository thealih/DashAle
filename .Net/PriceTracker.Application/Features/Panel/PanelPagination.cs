namespace PriceTracker.Application.Features.Panel;

public static class PanelPagination
{
    internal static bool IsValid(int page, int size, int maxSize) =>
        page >= 1 && size >= 1 && size <= maxSize && (long)(page - 1) * size <= int.MaxValue;

    public static int Offset(int page, int size) => (page - 1) * size;
}
