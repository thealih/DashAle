using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.Models.DTOs.ExDTOs;
using PriceTracker.Domain.Models.DTOs.ExDTOs.Users;

namespace PriceTracker.Domain.IServices
{
    public interface IExSsoService
    {
        Task<ExResponseDTO<List<GetUserResponseDTO>>> SearchUsersAsync(SearchUserRequestDTO parameters);
    }
}
