using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.IRepositories;
using PriceTracker.Domain.IRepositories.Common;

namespace PriceTracker.Infrastracture.Repositories.Common
{
    public class UnitOfWork : IUnitOfWork
    {
        IUserRepository? _userRep { get; set; }
        private readonly AppDbContext _dbContext;

        public UnitOfWork(
            AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IUserRepository UserRep => _userRep ??= new UserRepository(_dbContext);


        public async Task<int> SaveAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dbContext.Dispose();
            }
        }
    }
}
