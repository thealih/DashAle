using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Application.Models.DTOs;
using PriceTracker.Application.Models.Options;
using PriceTracker.Application.Models.Shared;
using PriceTracker.Application.Services;
using PriceTracker.Domain.IRepositories.Common;
using PriceTracker.Domain.IServices;
using PriceTracker.Domain.Models.DTOs.ExDTOs;
using PriceTracker.Domain.Models.Enums;

namespace PriceTracker.Application.Features.Queries.ExternalSample
{
    public class GetExternalSampleQuery:IRequest<ResponseDTO<List<PersonDTO>>>
    {
    }

    public class GetExternalSampleQueryHandler : IRequestHandler<GetExternalSampleQuery, ResponseDTO<List<PersonDTO>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IExternalService _externalService;
        private readonly IMapper _mapper;
        private readonly ExternalOption _options;
        private readonly ILogger<GetExternalSampleQueryHandler> _logger;

        public GetExternalSampleQueryHandler(
            IUnitOfWork unitOfWork,
            IExternalService externalService,
            IMapper mapper,
            IOptions<ExternalOption> options,
            ILogger<GetExternalSampleQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _externalService = externalService;
            _mapper = mapper;
            _options = options.Value;
            _logger = logger;
        }
        public async Task<ResponseDTO<List<PersonDTO>>> Handle(GetExternalSampleQuery request, CancellationToken cancellationToken)
        {
            //set input
            int id = 3;
            string url = $"/{_options.PersonUrl}/3";
            var authConfig = new AuthConfigDTO
            {
                AuthType = (AuthTypeEnum)_options.AuthType,
                Username = _options.UserName,
                Password = _options.Password
            };

            var result = new List<PersonDTO>();

            try
            {
                //send to service
                var response = await _externalService.CallServiceWithMappingAsync<PersonDTO>(
                _options.BaseUrl,
                url,
                null,
                HttpMethod.Get,
                authConfig: authConfig,
                mappings: _options.Mappings);

                result = (List<PersonDTO>)response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(GetExternalSampleQuery)}- error in externalService,msg is {ex.Message},{ex.InnerException.Message}");
                Response.IsFailure();
            }

            //output
            if (result is null || !result.Any())
                Response.IsFailure();

            return Response.IsSuccess(result);
        }
    }
}
