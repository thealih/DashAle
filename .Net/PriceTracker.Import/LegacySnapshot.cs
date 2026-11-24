using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture.Configurations;

namespace PriceTracker.Import;

internal sealed record TableMap(string Name, string[] Columns, Func<IEnumerable<object?[]>> Rows)
{
    public string ColumnList => string.Join(", ", Columns.Select(x => $"[{x}]"));
}

internal sealed class LegacySnapshot(SqliteConnection source, SqliteTransaction snapshot)
{
    private static readonly DateTime SeedDate = InitialReferenceData.Brands[0].CreatedAt;
    private static readonly Dictionary<string, Guid> BrandIds = InitialReferenceData.Brands.ToDictionary(x => x.Slug, x => x.Id);
    private static readonly Dictionary<string, Guid> SettingIds = InitialReferenceData.AppSettings.ToDictionary(x => x.Key, x => x.Id);
    private readonly HashSet<string> _settingKeys = ReadSettingKeys(source, snapshot);
    public int ResetRunningJobs { get; private set; }
    public int SkippedHeartbeats { get; private set; }

    public TableMap[] Tables() =>
    [
        Map("Brands", "Slug Name EnglishName Aliases LastDiscoveredAt DiscoveryError", () => Read("brands", r =>
            Row(Brand(r.Text("id")), SeedDate, false, r.Text("id"), r.Text("name"), r.Text("english"),
                r.Text("aliases"), r.Date("last_discovered"), r.OptionalText("discovery_error")))),
        Map("Sellers", "SourceId Name", () => Read("sellers", r =>
            Row(Key("seller", r.Text("id")), SeedDate, false, r.Text("id"), r.Text("name")))),
        Map("Products", "PublicId BrandId SourceId Title Url ImageUrl IsTracked DeletedAt LastAttemptAt LastSuccessAt NextCheckAt LeaseUntil Error FailureCount",
            () => Read("products", r =>
            {
                var id = r.Id("id");
                var sourceId = r.Text("source_id");
                var deleted = r.Date("deleted_at");
                var tracked = deleted is null && r.Bool("tracked");
                var url = sourceId.Length is >= 3 and <= 30 && sourceId.All(char.IsAsciiDigit)
                    ? $"https://snappshop.ir/product/snp-{sourceId}" : r.Text("url");
                return Row(Key("product", id), r.RequiredDate("created_at"), deleted != null,
                    id, Brand(r.Text("brand_id")), sourceId, r.Text("title"), url, r.OptionalText("image"),
                    tracked, deleted, r.Date("last_attempt"), r.Date("last_success"),
                    deleted == null ? r.Date("next_check") : null, null, r.OptionalText("error"),
                    checked((int)r.Integer("failure_count")));
            })),
        Map("LatestOffers", "ProductId SellerId OfferKey ComparisonKey Variant Price ListPrice Available ObservedAt IsMissing",
            () => Read("latest_offers", r =>
            {
                var at = r.RequiredDate("observed_at");
                return Row(Key("latest", r.Id("id")), at, false, Key("product", r.Id("product_id")),
                    Key("seller", r.Text("seller_id")), r.Text("offer_key"), r.OptionalText("comparison_key"),
                    r.Text("variant"), r.OptionalInteger("price"), r.OptionalInteger("list_price"),
                    r.OptionalBool("available"), at, r.Bool("missing"));
            })),
        Map("PriceObservations", "ProductId SellerId OfferKey Variant Price ListPrice Available ObservedAt",
            () => Read("observations", r =>
            {
                var at = r.RequiredDate("observed_at");
                return Row(Key("observation", r.Id("id")), at, false, Key("product", r.Id("product_id")),
                    Key("seller", r.Text("seller_id")), r.Text("offer_key"), r.Text("variant"),
                    r.OptionalInteger("price"), r.OptionalInteger("list_price"), r.OptionalBool("available"), at);
            })),
        Map("Notifications", "PublicId Kind BrandId ProductId SellerId Title OldPrice NewPrice Percent PreviousObservedAt IsRead IsAlert",
            () => Read("notifications", r =>
            {
                var id = r.Id("id");
                return Row(Key("notification", id), r.RequiredDate("created_at"), false, id, (int)NoticeKind(r.Text("kind")),
                    r.OptionalText("brand_id") is string brand ? Brand(brand) : null,
                    r.OptionalId("product_id") is long product ? Key("product", product) : null,
                    r.OptionalText("seller_id") is string seller ? Key("seller", seller) : null, r.Text("title"),
                    r.OptionalInteger("old_price"), r.OptionalInteger("new_price"), r.OptionalDouble("percent"),
                    r.Date("previous_observed_at"), r.Bool("read"), r.Bool("alerts"));
            })),
        Map("DiscoveryTasks", "BrandId Kind Status FinishedAt LeaseUntil Message", ReadJobs),
        Map("AppSettings", "Key Value", ReadSettings)
    ];

    public static IEnumerable<object?[]> SeedRows(string table) => table switch
    {
        "Brands" => InitialReferenceData.Brands.Select(x => Row(x.Id, x.CreatedAt, false,
            x.Slug, x.Name, x.EnglishName, x.Aliases, null, null)),
        "AppSettings" => InitialReferenceData.AppSettings.Select(x => Row(x.Id, x.CreatedAt, false, x.Key, x.Value)),
        _ => []
    };

    private IEnumerable<object?[]> ReadJobs()
    {
        ResetRunningJobs = 0;
        return Read("jobs", r =>
        {
            var id = r.Text("id");
            if (r.Text("kind") != "discovery")
                throw new InvalidDataException("Unknown discovery job kind.");
            var status = r.Text("status");
            var mapped = status switch
            {
                "running" or "queued" => DiscoveryTaskStatus.Queued,
                "done" => DiscoveryTaskStatus.Done,
                "failed" => DiscoveryTaskStatus.Failed,
                _ => throw new InvalidDataException("Unknown discovery job status.")
            };
            if (status == "running")
                ResetRunningJobs++;
            return Row(Guid.TryParse(id, out var guid) ? guid : Key("job", id), r.RequiredDate("created_at"), false,
                r.OptionalText("brand_id") is string brand ? Brand(brand) : null, (int)DiscoveryTaskKind.Discovery,
                (int)mapped, mapped == DiscoveryTaskStatus.Queued ? null : r.Date("finished_at"), null, r.OptionalText("message"));
        });
    }

    private IEnumerable<object?[]> ReadSettings()
    {
        SkippedHeartbeats = _settingKeys.Count(x => x is "worker_heartbeat" or "discovery_heartbeat");
        foreach (var item in Read("settings", r =>
        {
            var key = r.Text("key");
            if (key == ImportRun.MarkerKey)
                throw new InvalidDataException("Reserved import metadata key exists in source.");
            return Row(Setting(key), SeedDate, false, key, r.Text("value"));
        }))
            if (item[6] is not ("worker_heartbeat" or "discovery_heartbeat"))
                yield return item;

        foreach (var seed in InitialReferenceData.AppSettings)
            if (!_settingKeys.Contains(seed.Key))
                yield return Row(seed.Id, seed.CreatedAt, false, seed.Key, seed.Value);
    }

    private static HashSet<string> ReadSettingKeys(SqliteConnection source, SqliteTransaction snapshot)
    {
        using var query = source.CreateCommand();
        query.Transaction = snapshot;
        query.CommandText = "SELECT key FROM settings";
        using var reader = query.ExecuteReader();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
            keys.Add(reader.GetString(0));
        return keys;
    }

    private IEnumerable<object?[]> Read(string table, Func<LegacyRow, object?[]> map)
    {
        using var command = source.CreateCommand();
        command.Transaction = snapshot;
        command.CommandText = $"SELECT * FROM [{table}]";
        using var reader = command.ExecuteReader();
        var row = new LegacyRow(reader);
        var number = 0;
        while (reader.Read())
        {
            number++;
            object?[] values;
            try { values = map(row); }
            catch (Exception ex) when (ex is FormatException or InvalidDataException or InvalidCastException or OverflowException)
            {
                throw new InvalidDataException($"Invalid data in SQLite table {table}, row {number}: {ex.Message}", ex);
            }
            yield return values;
        }
    }

    private static TableMap Map(string table, string fields, Func<IEnumerable<object?[]>> rows) =>
        new(table, ["Id", "CreatedAt", "ModifiedAt", "CreatedBy", "ModifiedBy", "IsDeleted", .. fields.Split(' ')], rows);

    private static object?[] Row(Guid id, DateTime at, bool deleted, params object?[] values) =>
        [id, at, at, null, null, deleted, .. values];

    private static Guid Brand(string slug) => BrandIds.TryGetValue(slug, out var id) ? id : Key("brand", slug);
    private static Guid Setting(string key) => SettingIds.TryGetValue(key, out var id) ? id : Key("setting", key);
    private static Guid Key(string table, long id) => Key(table, id.ToString(CultureInfo.InvariantCulture));
    private static Guid Key(string table, string id) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"PriceTracker.SQLite.v1/{table}/{id}")).AsSpan(0, 16));

    private static NotificationKind NoticeKind(string value) => value switch
    {
        "seller_added" => NotificationKind.SellerAdded,
        "price_up" => NotificationKind.PriceUp,
        "price_down" => NotificationKind.PriceDown,
        "stock" => NotificationKind.Stock,
        "error" => NotificationKind.Error,
        "product_found" => NotificationKind.ProductFound,
        _ => throw new InvalidDataException("Unknown notification kind.")
    };
}

internal sealed class LegacyRow(SqliteDataReader reader)
{
    private readonly Dictionary<string, int> _fields = Enumerable.Range(0, reader.FieldCount)
        .ToDictionary(reader.GetName, x => x, StringComparer.Ordinal);

    private object? Value(string name)
    {
        if (_fields.TryGetValue(name, out var i))
            return reader.IsDBNull(i) ? null : reader.GetValue(i);
        return name is "deleted_at" or "comparison_key" ? null : throw Missing(name);
    }

    public string Text(string name) => OptionalText(name) ?? throw Missing(name);
    public string? OptionalText(string name) => Value(name) switch
    {
        null => null, string value => value, _ => throw Missing(name)
    };
    public long Integer(string name) => OptionalInteger(name) ?? throw Missing(name);
    public long? OptionalInteger(string name) => Value(name) switch
    {
        null => null, long value => value, _ => throw Missing(name)
    };
    public double? OptionalDouble(string name) => Value(name) switch
    {
        null => null, long value => value, double value when double.IsFinite(value) => value, _ => throw Missing(name)
    };
    public long Id(string name) => OptionalId(name) ?? throw Missing(name);
    public long? OptionalId(string name) => OptionalInteger(name) switch
    {
        null => null, long value when value > 0 => value, _ => throw Missing(name)
    };
    public bool Bool(string name) => OptionalBool(name) ?? throw Missing(name);
    public bool? OptionalBool(string name) => OptionalInteger(name) switch
    {
        null => null, 0 => false, 1 => true, _ => throw Missing(name)
    };
    public DateTime RequiredDate(string name) => Date(name) ?? throw Missing(name);
    public DateTime? Date(string name)
    {
        var value = OptionalText(name);
        if (value == null)
            return null;
        var zoned = value.EndsWith('Z') || (value.Length >= 6 && value[^6] is '+' or '-' && value[^3] == ':');
        return zoned && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time.UtcDateTime : throw Missing(name);
    }
    private static InvalidDataException Missing(string field) => new($"Missing or invalid {field}.");
}
