using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.Models.Entities.Common;
using PriceTracker.Domain.Models.Enums;

namespace PriceTracker.Domain.Models.DTOs.ExDTOs.Users
{
    public class SearchUserRequestDTO
    {
        //نام کاربری
        public List<string> UserNames { get; set; }

        public bool HasDetail { get; set; }

        public List<string> ParamDetail { get; set; } = new List<string>()
        {
            SearchUserParamEnum.FirstName.ToString(),
            SearchUserParamEnum.LastName.ToString(),
            SearchUserParamEnum.NationalCode.ToString(),
            SearchUserParamEnum.PhoneNumber.ToString(),
            SearchUserParamEnum.Type.ToString(),
            SearchUserParamEnum.EconomicNumber.ToString(),
            SearchUserParamEnum.PostalCode.ToString(),
            SearchUserParamEnum.Address.ToString()
        };

        public PageModel? Paging { get; set; }
    }

    enum SearchUserParamEnum
    {
        FirstName,
        LastName,
        Gender,
        PhoneNumber,
        Email,
        Degree,
        Field,
        State,
        SharifId,
        BirthDate,
        Relation,
        Type,
        NationalCode,
        EconomicNumber,
        PostalCode,
        Address
    }
}
