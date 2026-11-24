using System.ComponentModel.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Domain.Models.Enums
{
    public enum UserIncludeEnum
    {
        [Display(Name = "بدون اطلاعات تکمیلی")]
        None
    }
}
