using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Domain.Models.Entities.Common
{
    public class BaseReadEntity
    {
        public string Id { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
