using System.ComponentModel.DataAnnotations;
namespace PriceTracker.Domain.Models.Enums;

public enum NotificationKind
{
    [Display(Name = "فروشندهٔ جدید")]
    SellerAdded = 1,

    [Display(Name = "افزایش قیمت")]
    PriceUp = 2,

    [Display(Name = "کاهش قیمت")]
    PriceDown = 3,

    [Display(Name = "تغییر موجودی")]
    Stock = 4,

    [Display(Name = "خطا")]
    Error = 5,

    [Display(Name = "محصول جدید")]
    ProductFound = 6
}
