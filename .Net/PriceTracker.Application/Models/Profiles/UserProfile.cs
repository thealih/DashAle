using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Application.Models.DTOs;
using PriceTracker.Application.Models.Shared;
using PriceTracker.Domain.Models.DTOs;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Entities.Common;

namespace PriceTracker.Application.Models.Profiles
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<PageModel, PageDTO>()
                .ForMember(dest => dest.PageIndex, opt => opt.MapFrom(src => src.PageNumber))
                .ReverseMap();
            CreateMap<SearchModel, SearchDTO>().ReverseMap();

            CreateMap<User, UserDTO>().ReverseMap();
            CreateMap<UserSearchModel, UserSearchDTO>().ReverseMap();
            CreateMap<PagedResult<User>, PagedResult<UserDTO>>().ReverseMap();

        }
    }
}
