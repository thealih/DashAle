using PriceTracker.Domain.Models.Entities.Common;
using PriceTracker.Domain.Models.Enums;

namespace PriceTracker.Domain.Models.Entities;

public class DiscoveryTask : BaseEntity
{
    public Guid? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public DiscoveryTaskKind Kind { get; set; } = DiscoveryTaskKind.Discovery;
    public DiscoveryTaskStatus Status { get; set; } = DiscoveryTaskStatus.Queued;
    public DateTime? FinishedAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public string? Message { get; set; }
}
