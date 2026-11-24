using System.Linq.Expressions;

namespace PriceTracker.Domain.IRepositories.Common;

public interface IKeyedRepository<TEntity, TKey> where TEntity : class where TKey : notnull
{
    Task<TEntity?> GetByIdAsync(TKey id);
    Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> predicate, bool traking = false,
        params Expression<Func<TEntity, object>>[] includes);
    IQueryable<TEntity> GetAllQueryableAsync(bool tracking = false);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null);
    Task<int> GetCountAsync(Expression<Func<TEntity, bool>>? predicate = null);
    void Insert(TEntity entity);
    void Insert(List<TEntity> entities);
    void Update(TEntity entity);
    void Delete(TEntity entity);
    Task SaveChangesAsync();
}
