using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Application.Models.DTOs;
using PriceTracker.Application.Models.Shared;
using PriceTracker.Domain.IRepositories.Common;
using PriceTracker.Domain.Models.DTOs;

namespace PriceTracker.Application.Features.Queries.Users.GetAll
{
    public class UserSearchQuery : SearchDTO,IRequest<ResponseDTO<List<UserDTO>>>
    {
    }

    public class UserSearchQueryHandler : IRequestHandler<UserSearchQuery, ResponseDTO<List<UserDTO>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<UserSearchQueryHandler> _logger;

        public UserSearchQueryHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<UserSearchQueryHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }
        public async Task<ResponseDTO<List<UserDTO>>> Handle(UserSearchQuery request, CancellationToken cancellationToken)
        {
            //prepare model
            var searchModel=_mapper.Map<UserSearchModel>(request);

            //get data from db
            var userData = await _unitOfWork.UserRep.SearchUserAsync(searchModel);

            //map
            var users = _mapper.Map<List<UserDTO>>(userData);

            return Response.IsSuccess(users);
        }
    }
}
