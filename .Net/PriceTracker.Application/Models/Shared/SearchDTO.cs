using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Application.Models.Shared
{
    public class SearchDTO
    {
        public PageDTO Page { get; set; } = new PageDTO(0, 0);
    }
}
