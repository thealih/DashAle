using System.ComponentModel.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Domain.Models.Enums
{
    public enum OrderTypeEnum
    {
        [Display(Name = "صعودی")]
        Ascending = 1,

        [Display(Name = "نزولی")]
        Descending = 2
    }
}
