using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.IRepositories.Common;
using PriceTracker.Domain.Models.DTOs;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Entities.Common;

namespace PriceTracker.Domain.IRepositories
{
    public interface IUserRepository : IRepository<User>
    {
        Task<PagedResult<User>> SearchUserAsync(UserSearchModel model);
    }
}
