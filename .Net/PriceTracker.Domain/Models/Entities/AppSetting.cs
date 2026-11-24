using PriceTracker.Domain.Models.Entities.Common;

namespace PriceTracker.Domain.Models.Entities;

public class AppSetting : BaseEntity
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}
