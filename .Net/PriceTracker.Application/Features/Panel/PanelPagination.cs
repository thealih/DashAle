namespace PriceTracker.Application.Features.Panel;

public static class PanelPagination
{
    public static int Offset(int page, int size, int maxSize)
    {
        if (page < 1 || size < 1 || size > maxSize || (long)(page - 1) * size > int.MaxValue)
            throw new PanelException(422, "شماره یا اندازهٔ صفحه معتبر نیست.");
        return (page - 1) * size;
    }
}
