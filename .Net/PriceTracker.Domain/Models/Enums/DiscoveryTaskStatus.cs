using System.ComponentModel.DataAnnotations;
namespace PriceTracker.Domain.Models.Enums;

public enum DiscoveryTaskStatus
{
    [Display(Name = "در صف")]
    Queued = 1,

    [Display(Name = "در حال اجرا")]
    Running = 2,

    [Display(Name = "انجام شد")]
    Done = 3,

    [Display(Name = "ناموفق")]
    Failed = 4
}
