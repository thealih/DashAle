using PriceTracker.Domain.Models.Entities.Common;

namespace PriceTracker.Domain.Models.Entities;

public class Product : BaseEntity
{
    public long PublicId { get; set; }
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public required string SourceId { get; set; }
    public required string Title { get; set; }
    public required string Url { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsTracked { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? NextCheckAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public string? Error { get; set; }
    public int FailureCount { get; set; }

    public void Archive(DateTime deletedAtUtc)
    {
        IsDeleted = true;
        DeletedAt ??= deletedAtUtc;
        IsTracked = false;
        NextCheckAt = null;
        LeaseUntil = null;
    }

    public void Restore()
    {
        if (!IsDeleted)
            return;

        IsDeleted = false;
        DeletedAt = null;
        IsTracked = false;
        NextCheckAt = null;
        LeaseUntil = null;
    }
}
