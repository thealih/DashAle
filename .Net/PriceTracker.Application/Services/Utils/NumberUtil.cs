using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Application.Services.Utils
{
    public static class NumberUtil
    {
        public static int Generate(int from, int to)
        {
            Random rnd = new();
            return rnd.Next(from, to);
        }

        public static int Generate(int digit)
        {
            var from = Convert.ToInt32(Math.Pow(10, digit - 1));
            var to = Convert.ToInt32(Math.Pow(10, digit) - 1);
            Random rnd = new();
            return rnd.Next(from, to);
        }
    }
}
