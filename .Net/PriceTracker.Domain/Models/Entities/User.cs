using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.Models.Entities.Common;

namespace PriceTracker.Domain.Models.Entities
{
    public class User:BaseEntity
    {
        public string UserName { get; set; }
    }
}
