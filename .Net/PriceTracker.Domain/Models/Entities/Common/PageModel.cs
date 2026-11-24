using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Domain.Models.Entities.Common
{
    public class PageModel
    {
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
    }
}
