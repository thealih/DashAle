using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Application.Services.Utils
{
    public static class GuidUtil
    {
        public static bool IsGuid(this string value)
        {
            try
            {
                Guid x;
                return Guid.TryParse(value, out x);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsNullOrEmpty(this Guid guid) =>
            guid == null || guid == Guid.Empty;
    }
}
