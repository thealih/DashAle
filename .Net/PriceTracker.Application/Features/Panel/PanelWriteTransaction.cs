using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Infrastracture;
using System.Data;

namespace PriceTracker.Application.Features.Panel;

internal static class PanelWriteTransaction
{
    internal static async Task<T> RunAsync<T>(AppDbContext db, Func<Task<T>> operation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var result = await operation();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (Exception ex) when ((ex as SqlException ?? ex.InnerException as SqlException) is
            { Number: 1205 or 2601 or 2627 })
        {
            throw new PanelException(409, "درخواست هم‌زمان دیگری ثبت شد؛ دوباره تلاش کنید.");
        }
    }
}
