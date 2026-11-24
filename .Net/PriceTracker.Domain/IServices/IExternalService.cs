using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.Models.DTOs.ExDTOs;

namespace PriceTracker.Domain.IServices
{
    public interface IExternalService
    {
        Task<object> CallServiceWithMappingAsync<TResponse>(string baseAddress, string endpoint, Dictionary<string, object> requestData, HttpMethod method, Dictionary<string, string> mappings = null, Dictionary<string, string> headers = null, string nestedPropertyPath = null, AuthConfigDTO authConfig = null) where TResponse : class, new();
    }
}
