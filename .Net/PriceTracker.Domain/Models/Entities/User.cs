using System.Text.Json.Serialization;
using PriceTracker.Domain.Models.Entities.Common;
using PriceTracker.Domain.Models.Enums;

namespace PriceTracker.Domain.Models.Entities;

public class User : BaseEntity
{
    public required string UserName { get; set; }

    [JsonIgnore]
    public string PasswordHash { get; set; } = null!;

    public UserRole Role { get; set; } = UserRole.Viewer;
    public bool IsActive { get; set; } = true;
}
