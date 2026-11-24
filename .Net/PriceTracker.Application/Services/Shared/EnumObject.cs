using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Application.Services.Shared
{
    public class EnumObject<T>
    {
        public string Title { get; set; }
        public T Value { get; set; }
    }
}
