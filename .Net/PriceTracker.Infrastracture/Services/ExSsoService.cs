using IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Infrastracture.Models.Statics;
using PriceTracker.Infrastracture.Models;
using PriceTracker.Infrastracture.Models.Options;
using PriceTracker.Domain.IServices;
using PriceTracker.Domain.Models.DTOs.ExDTOs.Users;
using PriceTracker.Domain.Models.DTOs.ExDTOs;

namespace PriceTracker.Infrastracture.Services
{
    public class ExSsoService : IExSsoService
    {
        private readonly ApiOptions _options;
        private readonly ILogger<ExSsoService> _logger;
        private RestClient _client;

        public ExSsoService(
            IOptions<ApiOptions> options,
            ILogger<ExSsoService> logger)
        {
            _options = options.Value;

            var _clientOptions = new RestClientOptions(_options.BaseUrl);
            _client = new RestClient(_clientOptions);

            _logger = logger;
        }

        private async Task<ExResponseDTO<string>> GetTokenAsync()
        {
            var response = new ExResponseDTO<string>();

            // discover endpoints from metadata
            var client = new HttpClient();
            var disco = await client.GetDiscoveryDocumentAsync(_options.SSOUrl);
            if (disco.IsError)
            {
                _logger.LogError($"UserExternalService_GetTokenAsync - message : {disco.Error}");
                response.Message = ExternalErrorMessages.ErrorToken;
            }

            //get token
            var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = disco.TokenEndpoint,
                ClientId = _options.ClientId,
                ClientSecret = _options.ClientSecret,
            });

            if (tokenResponse.IsError)
            {
                _logger.LogError($"UserExternalService_GetTokenAsync - message : {tokenResponse.Error}");
                response.Message = ExternalErrorMessages.ErrorToken;
            }

            response.Data = tokenResponse.AccessToken;
            response.IsSuccess = true;

            return response;
        }

        public async Task<ExResponseDTO<List<GetUserResponseDTO>>> SearchUsersAsync(SearchUserRequestDTO parameters)
        {

            //prepare request
            string url = $"/{_options.SearchUserUrl}";
            var request = new RestRequest(url);

            //set body
            request.AddJsonBody(parameters);

            //response
            var responseSso = await PostOrPutAsync<SearchUserResponseDTO>(request, nameof(SearchUsersAsync));

            var response = new ExResponseDTO<List<GetUserResponseDTO>>()
            {
                Data = responseSso.Data.Data,
                IsSuccess = responseSso.IsSuccess,
                Message = responseSso.Message,
            };

            return response;
        }



        public async Task<ExResponseDTO<TResponse>> PostOrPutAsync<TResponse>(RestRequest request, string methodName, bool? put = false)
        {
            var output = new ExResponseDTO<TResponse>();

            //get token
            var tokenResponse = await GetTokenAsync();
            if (!tokenResponse.IsSuccess)
            {
                output.Message = tokenResponse.Message;
                return output;
            }

            string token = tokenResponse.Data;

            //add headers
            request.AddHeader(HeaderStatic.Authorization, $"Bearer {token}");

            //response
            try
            {
                var response = new ExResponseDTO<TResponse>();

                if (!put.Value)
                    response = await _client.PostAsync<ExResponseDTO<TResponse>>(request);
                else
                    response = await _client.PutAsync<ExResponseDTO<TResponse>>(request);

                if (response.IsSuccess)
                {
                    output.IsSuccess = true;
                    output.Data = response.Data;
                }
                else
                    output.Message = response.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(ExSsoService)}_{methodName} - message: {ex.Message}");
                output.Message = ExternalErrorMessages.ErrorApiService;
            }

            return output;
        }

        public async Task<ExResponseDTO<TResponse>> GetAsync<TResponse>(RestRequest request, string methodName)
        {
            var output = new ExResponseDTO<TResponse>();

            //get token
            var tokenResponse = await GetTokenAsync();
            if (!tokenResponse.IsSuccess)
            {
                output.Message = tokenResponse.Message;
                return output;
            }

            string token = tokenResponse.Data;

            //add headers
            request.AddHeader(HeaderStatic.Authorization, $"Bearer {token}");

            //response
            try
            {
                var response = await _client.GetAsync<ExResponseDTO<TResponse>>(request);
                if (response.IsSuccess)
                {
                    output.IsSuccess = true;
                    output.Data = response.Data;
                }
                else
                    output.Message = response.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(ExSsoService)}_{methodName} - message: {ex.Message}");
                output.Message = ExternalErrorMessages.ErrorApiService;
            }

            return output;
        }
    }
}
