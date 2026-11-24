using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.Models.Entities.Common;

namespace PriceTracker.Infrastracture.Services.Shared
{
    public static class Paging
    {
        public static async Task<PagedResult<T>> GetPaged<T>(this IQueryable<T> query,
                                             int pageNumber, int pageSize) where T : class
        {
            if (pageNumber == 0)
                pageNumber = 1;

            if (pageSize == 0)
                pageSize = 10;

            var result = new PagedResult<T>()
            {
                PageIndex = pageNumber,
                PageSize = pageSize,
                RowCount = await query.CountAsync()
            };
            result.PageIndex = pageNumber;
            result.PageSize = pageSize;
            result.RowCount = await query.CountAsync();


            //var pageCount = (double)result.TotalCount / pageSize;
            //result.PageCount = (int)Math.Ceiling((double)pageCount);

            var skip = (pageNumber - 1) * pageSize;
            result.Data = await query.Skip(skip).Take(pageSize).ToListAsync();

            return result;
        }
    }
}
