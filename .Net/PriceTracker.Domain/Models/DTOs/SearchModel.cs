using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.Models.Entities.Common;
using PriceTracker.Domain.Models.Enums;

namespace PriceTracker.Domain.Models.DTOs
{
    public class SearchModel
    {
        public Guid? Id { get; set; }
        public bool? Tracking { get; set; } = false;
        public PageModel Page { get; set; } = new PageModel
        {
            PageNumber = 1,
            PageSize = int.MaxValue
        };

        public List<UserIncludeEnum> Includes { get; set; }

    }
}
