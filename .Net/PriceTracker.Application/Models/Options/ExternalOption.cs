using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Application.Models.Options
{
    public class ExternalOption
    {
        public string BaseUrl { get; set; }
        public string PersonUrl { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public int AuthType { get; set; }
        public Dictionary<string, string> Mappings { get; set; }

    }
}
