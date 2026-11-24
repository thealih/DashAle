using AutoMapper;
using PriceTracker.Api.Models.Shared;
using PriceTracker.Api.Models.ViewModels;
using PriceTracker.Application.Models.DTOs;
using PriceTracker.Application.Models.Shared;

namespace PriceTracker.Api.Models
{
    public class ApiMappingProfile : Profile
    {
        public ApiMappingProfile()
        {
            CreateMap<PageVm, PageDTO>().ReverseMap();
            CreateMap<SearchVm, SearchDTO>().ReverseMap();

            CreateMap<UserSearchVm, UserSearchDTO>().ReverseMap();
        }
    }
}
