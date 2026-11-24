using System.ComponentModel.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Domain.Models.Enums
{
    public enum GenderEnum
    {
        [Display(Name = "زن")]
        Woman = 0,

        [Display(Name = "مرد")]
        Man = 1
    }
}
