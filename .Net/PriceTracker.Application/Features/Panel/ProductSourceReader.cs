using PriceTracker.Domain.IServices;

namespace PriceTracker.Application.Features.Panel;

internal static class ProductSourceReader
{
    internal static async Task<SourceProduct> ReadAsync(IProductSource source, string id, CancellationToken ct)
    {
        try { return await source.GetProductAsync(id, ct); }
        catch (ProductSourceException ex) { throw new PanelException(502, ex.Message); }
    }
}
