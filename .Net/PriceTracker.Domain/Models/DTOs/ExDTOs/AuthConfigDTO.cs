using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.Models.Enums;

namespace PriceTracker.Domain.Models.DTOs.ExDTOs
{
    public class AuthConfigDTO
    {
        public AuthTypeEnum AuthType { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ApiKey { get; set; }
        public string ApiKeyHeader { get; set; } = "X-API-KEY";
        public string Token { get; set; }
    }
}
