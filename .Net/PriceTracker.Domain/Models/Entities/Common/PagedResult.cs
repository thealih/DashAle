using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Domain.Models.Entities.Common
{
    public class PagedResult<T> where T : class
    {
        public List<T> Data { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public long RowCount { get; set; }

        public PagedResult()
        {
            Data = new List<T>();
        }
    }
}
