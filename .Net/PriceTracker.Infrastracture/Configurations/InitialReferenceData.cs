using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Infrastracture.Configurations;

internal static class InitialReferenceData
{
    // Fixed IDs and dates keep EF migration snapshots stable between builds.
    private static readonly DateTime InitialTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    internal static readonly Brand[] Brands =
    [
        Brand(1, "vitalayer", "ویتالیر", "Vitalayer", "ویتالیر,ویتالایر,vitalayer"),
        Brand(2, "eudoramax", "ادورامکس", "Eudora Max", "ادورامکس,ادورا مکس,eudora max,eudoramax"),
        Brand(3, "elvina", "الوینا", "Elvina", "الوینا,elvina"),
        Brand(4, "isol", "آیسول", "Isol", "آیسول,ایسول,isol"),
        Brand(5, "vitaplex", "ویتاپلکس", "Vitaplex", "ویتاپلکس,ویتا پلکس,vitaplex"),
        Brand(6, "belfamed", "بلفامد", "Belfamed", "بلفامد,belfamed"),
        Brand(7, "bergamia", "برگامیا", "Bergamia", "برگامیا,bergamia")
    ];

    internal static readonly AppSetting[] AppSettings =
    [
        Setting(1, "interval_minutes", "60"),
        Setting(2, "threshold_percent", "0"),
        Setting(3, "retention_days", "30"),
        Setting(4, "discovery_hours", "24"),
        Setting(5, "undercut_toman", "1")
    ];

    private static Brand Brand(int id, string slug, string name, string englishName, string aliases) => new()
    {
        Id = new Guid(id, 0, 0, 0x80, 0, 0, 0, 0, 0, 0, 1),
        Slug = slug,
        Name = name,
        EnglishName = englishName,
        Aliases = aliases,
        CreatedAt = InitialTimestamp,
        ModifiedAt = InitialTimestamp
    };

    private static AppSetting Setting(int id, string key, string value) => new()
    {
        Id = new Guid(id, 0, 0, 0x80, 0, 0, 0, 0, 0, 0, 2),
        Key = key,
        Value = value,
        CreatedAt = InitialTimestamp,
        ModifiedAt = InitialTimestamp
    };
}
