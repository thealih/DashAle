using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using PriceTracker.Domain.Models.Entities.Common;

namespace PriceTracker.Domain.IRepositories.Common
{
    public interface IReadRepository<T> where T : BaseReadEntity
    {
        /// <summary>
        /// اضافه کردن موجودیت جدید
        /// </summary>
        /// <param name="entity"></param>
        Task<T> AddAsync(T entity);

        /// <summary>
        /// اضافه کردن لیستی از موجودیت ها
        /// </summary>
        /// <param name="entity"></param>
        Task AddManyAsync(IEnumerable<T> entities);

        IQueryable<T> Get(Expression<Func<T, bool>>? predicate = null);
        Task<List<T>> GetAllAsync(Expression<Func<T, bool>>? predicate = null);
        Task<T> GetAsync(Expression<Func<T, bool>> predicate);
        Task<T> GetByIdAsync(string id);
        Task<T> RemoveAsync(T entity);
        Task<T> RemoveAsync(string id);
        Task<T> RemoveAsync(Expression<Func<T, bool>> filter);
        Task RemoveManyAsync(List<string> ids);
        Task<T> UpdateAsync(string id, T entity);
        Task<T> UpdateAsync(T entity, Expression<Func<T, bool>> predicate);
    }
}
