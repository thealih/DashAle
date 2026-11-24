using System.ComponentModel.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Domain.Models.Enums
{
    public enum AuthTypeEnum
    {
        [Display(Name = "بدون احراز هویت")]
        None,

        [Display(Name = "توکن حامل")]
        BearerToken,

        [Display(Name = "احراز هویت پایه")]
        BasicAuth,

        [Display(Name = "کلید API")]
        ApiKey,

        [Display(Name = "احراز هویت با OAuth 2.0")]
        OAuth2,

        [Display(Name = "سفارشی")]
        Custom
    }
}
