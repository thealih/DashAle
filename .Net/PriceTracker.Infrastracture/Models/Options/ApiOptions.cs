using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Infrastracture.Models.Options
{
    public class ApiOptions
    {
        public string SSOUrl { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }

        public string BaseUrl { get; set; }
        public string SearchUserUrl { get; set; }
        public string AddClientUrl { get; set; }
        public string GetUserInfoUrl { get; set; }
    }
}
