using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Application.Models.DTOs
{
    public class ExceptionDetailDTO
    {
        public string? Class { get; set; }
        public string? Method { get; set; }
        public int? LineNumber { get; set; }
        public string? Message { get; set; }
        public string? InnerExceptionMessage { get; set; }
    }
}
