using AutoMapper;
using PriceTracker.Api.Models.Shared;
using PriceTracker.Application.Models.Shared;

namespace PriceTracker.Api.Models
{
    public class ApiMappingProfile : Profile
    {
        public ApiMappingProfile()
        {
            CreateMap<PageVm, PageDTO>().ReverseMap();
            CreateMap<SearchVm, SearchDTO>().ReverseMap();

        }
    }
}
