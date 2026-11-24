using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Infrastracture;

namespace PriceTracker.Api.Panel;

internal static class LocalDatabaseBackup
{
    internal static async Task WriteAsync(AppDbContext db, string destination)
    {
        var path = Path.GetFullPath(destination);
        if (!path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) || File.Exists(path))
            throw new InvalidOperationException("Choose a new .bak file.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await db.Database.OpenConnectionAsync();
        await using var cmd = (SqlCommand)db.Database.GetDbConnection().CreateCommand();
        var database = cmd.Connection!.Database.Replace("]", "]]", StringComparison.Ordinal);
        cmd.CommandText = $"BACKUP DATABASE [{database}] TO DISK = @path WITH COPY_ONLY, CHECKSUM;";
        cmd.CommandTimeout = 180;
        cmd.Parameters.Add("@path", SqlDbType.NVarChar, 4000).Value = path;
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"Backup created: {path}");
    }
}
