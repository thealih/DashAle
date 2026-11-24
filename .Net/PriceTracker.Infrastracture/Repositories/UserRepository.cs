using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.IRepositories;
using PriceTracker.Domain.Models.DTOs;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Entities.Common;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture.Repositories.Common;
using PriceTracker.Infrastracture.Services.Shared;

namespace PriceTracker.Infrastracture.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(AppDbContext context) : base(context)
        {
        }

        public Task<PagedResult<User>> SearchUserAsync(UserSearchModel model)
        {
            //get query
            var query = GetAllQueryableAsync(tracking: model.Tracking.Value);


            //----------------------------------------------- filtering -------------------------------------------------

            //id
            if (model.Id is not null)
                query = query.Where(x => x.Id == model.Id);

            //userName
            if (string.IsNullOrWhiteSpace(model.UserName))
                query = query.Where(x => model.UserName.Contains(x.UserName));


            //----------------------------------------------- end filtering ----------------------------------------------



            //--------------------------------------------------- includes -----------------------------------------------

            switch (model.Includes)
            {
                //case UserIncludeEnum.Legal:
                //    query = query.Include(x => x.UserLegal);
                //    break;
                
            }
            //--------------------------------------------------- end includes -------------------------------------------



            //---------------------------------------------------- orderby -----------------------------------------------

            query = query.OrderByDescending(x => x.CreatedBy);

            //--------------------------------------------------- end orderby --------------------------------------------


            return Paging.GetPaged(query, model.Page.PageNumber, model.Page.PageSize);
        }
    }
}
