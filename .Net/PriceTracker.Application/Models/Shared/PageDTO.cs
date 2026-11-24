using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Application.Models.Shared
{
    public class PageDTO
    {
        public PageDTO(int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
        }

        //pageNumber
        public int PageIndex { get; }
        public int PageSize { get; }
    }
}
