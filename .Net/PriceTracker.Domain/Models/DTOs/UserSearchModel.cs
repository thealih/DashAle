using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Domain.Models.DTOs
{
    public class UserSearchModel:SearchModel
    {
        public string? UserName { get; set; }
    }
}
