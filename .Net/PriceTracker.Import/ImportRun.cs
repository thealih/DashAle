using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace PriceTracker.Import;

internal sealed class ImportRun(SqlConnection target, TableMap[] tables, string sourceMode)
{
    internal const string MarkerKey = "_legacy_import_fingerprint";
    private const int MaxBatchParameters = 2_000;

    public static Dictionary<string, TableDigest> ReadSource(TableMap[] tables) =>
        tables.ToDictionary(t => t.Name, t => TableDigest.Compute(t.Rows()));

    public async Task<string> ExecuteAsync(bool apply, Dictionary<string, TableDigest> expected, CancellationToken ct)
    {
        await using var tx = (SqlTransaction)await target.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        using (var guard = Command("""
            DECLARE @result int;
            EXEC @result=sp_getapplock @Resource=N'PriceTracker.LegacyImport', @LockMode=N'Exclusive',
                @LockOwner=N'Transaction', @LockTimeout=15000;
            SELECT @result;
            """, tx))
        {
            if ((int)(await guard.ExecuteScalarAsync(ct))! < 0)
                throw new InvalidOperationException("Another import is running. Retry after it finishes.");
        }
        var marker = await ReadMarkerAsync(tx, ct);
        var actual = await ReadTargetAsync(tx, apply, ct);
        var incomingStamp = Stamp(expected);
        var existingStamp = Stamp(actual);
        if (marker != null && !marker.StartsWith($"sqlite-v1:{sourceMode}:", StringComparison.Ordinal))
            throw new InvalidOperationException("Destination belongs to a different source mode or import version.");

        if (!apply)
        {
            AssertMatch(expected, actual);
            if (marker != incomingStamp)
                throw new InvalidOperationException("Destination matches the rows but has no matching import marker.");
            await tx.CommitAsync(ct);
            return "verified";
        }

        if (marker == null)
        {
            var seeds = tables.ToDictionary(t => t.Name, t => TableDigest.Compute(LegacySnapshot.SeedRows(t.Name)));
            if (!Equal(seeds, actual))
                throw new InvalidOperationException("The destination is not a fresh, seeded PriceTracker database. Use a dedicated import database.");
        }
        else if (marker != existingStamp)
        {
            throw new InvalidOperationException("Destination changed after the prior import. No data was overwritten. Use a fresh import database.");
        }
        else if (existingStamp == incomingStamp)
        {
            await tx.CommitAsync(ct);
            return "already_current";
        }

        foreach (var table in tables.Reverse())
            await Command($"DELETE FROM dbo.[{table.Name}];", tx).ExecuteAndDisposeAsync(ct);
        foreach (var table in tables)
            await InsertAsync(table, tx, ct);

        var copied = await ReadTargetAsync(tx, false, ct);
        AssertMatch(expected, copied);

        // Keep explicit legacy PublicIds and ensure generated values cannot collide after migration.
        await Command("""
            DBCC CHECKIDENT ('dbo.Products', RESEED) WITH NO_INFOMSGS;
            DBCC CHECKIDENT ('dbo.Notifications', RESEED) WITH NO_INFOMSGS;
            """, tx).ExecuteAndDisposeAsync(ct);
        using var insert = Command("""
            INSERT dbo.AppSettings (Id, [Key], Value, CreatedAt, ModifiedAt, IsDeleted)
            VALUES (NEWID(), @key, @value, SYSUTCDATETIME(), SYSUTCDATETIME(), 0)
            """, tx);
        insert.Parameters.Add("@key", SqlDbType.NVarChar, 80).Value = MarkerKey;
        insert.Parameters.Add("@value", SqlDbType.NVarChar, 150).Value = incomingStamp;
        await insert.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return "imported_and_verified";
    }

    private async Task<string?> ReadMarkerAsync(SqlTransaction tx, CancellationToken ct)
    {
        using var command = Command("SELECT Value FROM dbo.AppSettings WHERE [Key]=@marker", tx);
        command.Parameters.Add("@marker", SqlDbType.NVarChar, 80).Value = MarkerKey;
        return (string?)await command.ExecuteScalarAsync(ct);
    }

    private async Task<Dictionary<string, TableDigest>> ReadTargetAsync(SqlTransaction tx, bool exclusive, CancellationToken ct)
    {
        var result = new Dictionary<string, TableDigest>();
        foreach (var table in tables)
        {
            var hint = exclusive ? " WITH (TABLOCKX, HOLDLOCK)" : "";
            var filter = table.Name == "AppSettings" ? " WHERE [Key] <> @marker" : "";
            using var command = Command($"SELECT {table.ColumnList} FROM dbo.[{table.Name}]{hint}{filter};", tx);
            if (filter.Length != 0)
                command.Parameters.Add("@marker", SqlDbType.NVarChar, 80).Value = MarkerKey;
            await using var reader = await command.ExecuteReaderAsync(ct);
            var digest = new TableDigest();
            var values = new object[reader.FieldCount];
            while (await reader.ReadAsync(ct))
            {
                reader.GetValues(values);
                digest.Add(values);
            }
            result.Add(table.Name, digest);
        }
        return result;
    }

    private async Task InsertAsync(TableMap table, SqlTransaction tx, CancellationToken ct)
    {
        SqlDbType[] types;
        using (var schema = Command($"SELECT TOP (0) {table.ColumnList} FROM dbo.[{table.Name}];", tx))
        await using (var reader = await schema.ExecuteReaderAsync(ct))
            types = Enumerable.Range(0, reader.FieldCount).Select(i => ParameterType(reader.GetFieldType(i))).ToArray();

        var keepPublicIds = table.Name is "Products" or "Notifications";
        if (keepPublicIds)
            await Command($"SET IDENTITY_INSERT dbo.[{table.Name}] ON;", tx).ExecuteAndDisposeAsync(ct);

        foreach (var batch in table.Rows().Chunk(MaxBatchParameters / types.Length))
        {
            ct.ThrowIfCancellationRequested();
            using var command = Command("", tx);
            var groups = new string[batch.Length];
            for (var row = 0; row < batch.Length; row++)
            {
                var names = new string[types.Length];
                for (var col = 0; col < names.Length; col++)
                {
                    var name = $"@p{row * types.Length + col}";
                    var parameter = command.Parameters.Add(name, types[col]);
                    if (types[col] == SqlDbType.NVarChar)
                        parameter.Size = -1;
                    if (types[col] == SqlDbType.DateTime2)
                        parameter.Scale = 7;
                    parameter.Value = batch[row][col] ?? DBNull.Value;
                    names[col] = name;
                }
                groups[row] = $"({string.Join(", ", names)})";
            }
            command.CommandText = $"INSERT dbo.[{table.Name}] ({table.ColumnList}) VALUES {string.Join(", ", groups)};";
            await command.ExecuteNonQueryAsync(ct);
        }

        if (keepPublicIds)
            await Command($"SET IDENTITY_INSERT dbo.[{table.Name}] OFF;", tx).ExecuteAndDisposeAsync(ct);
    }

    private static SqlDbType ParameterType(Type type) => Type.GetTypeCode(type) switch
    {
        TypeCode.String => SqlDbType.NVarChar,
        TypeCode.Int32 => SqlDbType.Int,
        TypeCode.Int64 => SqlDbType.BigInt,
        TypeCode.Boolean => SqlDbType.Bit,
        TypeCode.DateTime => SqlDbType.DateTime2,
        TypeCode.Double => SqlDbType.Float,
        _ when type == typeof(Guid) => SqlDbType.UniqueIdentifier,
        _ => throw new InvalidDataException($"Unsupported destination column type: {type.Name}.")
    };

    private string Stamp(Dictionary<string, TableDigest> data)
    {
        var stable = data.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new { Table = x.Key, x.Value.Count, x.Value.Hash });
        var bytes = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(stable));
        return $"sqlite-v1:{sourceMode}:{Convert.ToHexString(bytes)}";
    }

    private static bool Equal(Dictionary<string, TableDigest> expected, Dictionary<string, TableDigest> actual) =>
        expected.All(x => actual.TryGetValue(x.Key, out var found) && found.Hash == x.Value.Hash && found.Count == x.Value.Count);

    private static void AssertMatch(Dictionary<string, TableDigest> expected, Dictionary<string, TableDigest> actual)
    {
        var differences = expected.Where(x => !actual.TryGetValue(x.Key, out var found) ||
            found.Hash != x.Value.Hash || found.Count != x.Value.Count).Select(x => x.Key).ToArray();
        if (differences.Length != 0)
            throw new InvalidDataException("The destination differs from the source in: " + string.Join(", ", differences));
    }

    private SqlCommand Command(string sql, SqlTransaction tx) => new(sql, target, tx) { CommandTimeout = 120 };
}

internal sealed class TableDigest
{
    private readonly byte[] _hash = new byte[32];
    public long Count { get; private set; }
    public string Hash => Convert.ToHexString(_hash);

    public void Add(object?[] row)
    {
        // Row values include their primary key. XOR makes the digest independent of SQLite/SQL Server sort order.
        var normalized = row.Select(x => x switch
        {
            DBNull => null,
            DateTime date => DateTime.SpecifyKind(date, DateTimeKind.Utc),
            _ => x
        }).ToArray();
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(normalized));
        for (var i = 0; i < _hash.Length; i++)
            _hash[i] ^= hash[i];
        Count++;
    }

    public static TableDigest Compute(IEnumerable<object?[]> rows)
    {
        var digest = new TableDigest();
        foreach (var row in rows)
            digest.Add(row);
        return digest;
    }
}

internal static class ImportSql
{
    internal static async Task ExecuteAndDisposeAsync(this SqlCommand command, CancellationToken ct)
    {
        using (command)
            await command.ExecuteNonQueryAsync(ct);
    }
}
