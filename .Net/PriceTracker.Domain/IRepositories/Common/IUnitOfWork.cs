using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Domain.IRepositories.Common
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository UserRep { get; }


        Task<int> SaveAsync();
    }
}
