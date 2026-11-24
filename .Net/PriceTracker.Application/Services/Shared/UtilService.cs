using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Application.Services.Shared
{
    public static class UtilService
    {
        public static string GenerateUniqueNumber(int digit)
        {
            string number = DateTime.UtcNow.Ticks.ToString().Substring(0, digit);
            return number;
        }
    }
}
