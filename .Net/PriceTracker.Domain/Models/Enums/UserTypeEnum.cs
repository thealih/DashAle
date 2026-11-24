using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Domain.Models.Enums
{
    public enum UserTypeEnum
    {
        [Display(Name = "حقیقی")]
        Real = 1,

        [Display(Name = "حقوقی")]
        Legal
    }
}
