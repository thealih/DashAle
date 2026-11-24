using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Domain.Models.Entities.Common
{
    public interface IOrderBy
    {
        dynamic Expression { get; }
    }
}
