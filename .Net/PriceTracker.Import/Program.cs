using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Import;
using PriceTracker.Infrastracture;

if (args.Length < 2 || args[0] is not ("--plan" or "--apply" or "--verify") ||
    (args[0] == "--plan" ? args.Length != 2 : args.Length != 3))
{
    Console.Error.WriteLine("Usage: --plan <snapshot.db> | --apply <snapshot.db> <PriceTrackerImport...> | --verify <snapshot.db> <PriceTrackerImport...>");
    return 2;
}

using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
var ct = cancel.Token;
try
{
    var path = Path.GetFullPath(args[1]);
    if (!File.Exists(path))
        throw new InvalidOperationException("SQLite snapshot does not exist.");
    var input = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
    await using var sqlite = new SqliteConnection(input.ConnectionString);
    await sqlite.OpenAsync(ct);
    await using var snapshot = sqlite.BeginTransaction(deferred: true);
    using var check = sqlite.CreateCommand();
    check.Transaction = snapshot;
    check.CommandText = "PRAGMA quick_check";
    if ((string?)await check.ExecuteScalarAsync(ct) != "ok")
        throw new InvalidDataException("SQLite integrity check did not pass.");
    check.CommandText = "PRAGMA foreign_key_check";
    await using (var violations = await check.ExecuteReaderAsync(ct))
        if (await violations.ReadAsync(ct))
            throw new InvalidDataException($"SQLite foreign key check failed for table {violations.GetString(0)}.");

    check.CommandText = """
        SELECT EXISTS(SELECT 1 FROM products WHERE source_id GLOB 'demo-*'),
               EXISTS(SELECT 1 FROM products WHERE source_id NOT GLOB 'demo-*')
        """;
    string mode;
    await using (var types = await check.ExecuteReaderAsync(ct))
    {
        await types.ReadAsync(ct);
        var demo = types.GetBoolean(0);
        var live = types.GetBoolean(1);
        if (demo && live)
            throw new InvalidDataException("The SQLite snapshot mixes demo and live products.");
        mode = demo ? "demo" : "live";
    }

    var legacy = new LegacySnapshot(sqlite, snapshot);
    var tables = legacy.Tables();
    var expected = ImportRun.ReadSource(tables);
    var outcome = "planned";
    string? database = null;
    if (args[0] != "--plan")
    {
        var connection = Environment.GetEnvironmentVariable("PRICE_TRACKER_IMPORT_SQL")
            ?? throw new InvalidOperationException("Set the PRICE_TRACKER_IMPORT_SQL connection string.");
        var sql = new SqlConnectionStringBuilder(connection);
        database = args[2];
        if (!Regex.IsMatch(database, @"\APriceTrackerImport[A-Za-z0-9_]+\z", RegexOptions.CultureInvariant)
            || !string.Equals(sql.InitialCatalog, database, StringComparison.Ordinal) || sql.AttachDBFilename.Length != 0)
            throw new InvalidOperationException("The confirmed database and connection must name the same dedicated PriceTrackerImport database.");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(sql.ConnectionString).Options;
        await using var context = new AppDbContext(options);
        if ((await context.Database.GetPendingMigrationsAsync(ct)).Any())
            throw new InvalidOperationException("Apply the current EF migrations to the dedicated destination before importing.");
        await using var target = new SqlConnection(sql.ConnectionString);
        await target.OpenAsync(ct);
        outcome = await new ImportRun(target, tables, mode).ExecuteAsync(args[0] == "--apply", expected, ct);
    }

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        outcome, database, mode, excludedHeartbeats = legacy.SkippedHeartbeats,
        recoveredRunningJobs = legacy.ResetRunningJobs, tables = expected
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception error) when (error is InvalidOperationException or InvalidDataException or ArgumentException
    or System.Data.Common.DbException or OperationCanceledException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
