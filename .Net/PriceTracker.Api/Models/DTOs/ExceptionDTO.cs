using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Api.Models.DTOs
{
    public class ExceptionDTO
    {
        public string? Class { get; set; }
        public string? Method { get; set; }
        public int? LineNumber { get; set; }
        public string? Message { get; set; }
        public string? InnerExceptionMessage { get; set; }
    }
}
