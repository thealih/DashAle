using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.Models.Enums;

namespace PriceTracker.Domain.Models.DTOs.ExDTOs.Users
{
    public class SearchUserResponseDTO
    {
        public List<GetUserResponseDTO> Data { get; set; } = new();
        public int CurrentPage { get; set; }
        public int PageCount { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int FirstRowOnPage { get; set; }
        public int LastRowOnPage { get; set; }
    }

    public class GetUserResponseDTO
    {
        public string UserName { get; set; }
        public UserDetailResponseDTO Details { get; set; }
    }

    public class UserDetailResponseDTO
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string NationalCode { get; set; }
        public string PhoneNumber { get; set; }
        public UserTypeEnum Type { get; set; }
        public string EconomicNumber { get; set; }
        public string PostalCode { get; set; }
        public string Address { get; set; }
    }
}
