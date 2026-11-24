using System.ComponentModel.DataAnnotations;
namespace PriceTracker.Domain.Models.Enums;

public enum UserRole
{
    [Display(Name = "مشاهده‌گر")]
    Viewer = 1,

    [Display(Name = "مدیر")]
    Admin = 2
}
