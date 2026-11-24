using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Domain.IRepositories.Common;

namespace PriceTracker.Infrastracture.Repositories.Common;

public abstract class KeyedRepository<TEntity, TKey>(AppDbContext context, Expression<Func<TEntity, TKey>> key)
    : IKeyedRepository<TEntity, TKey> where TEntity : class where TKey : notnull
{
    protected AppDbContext Context { get; } = context;
    protected DbSet<TEntity> DbSet { get; } = context.Set<TEntity>();

    public Task<TEntity?> GetByIdAsync(TKey id)
    {
        var match = Expression.Equal(key.Body, Expression.Constant(id, typeof(TKey)));
        return DbSet.FirstOrDefaultAsync(Expression.Lambda<Func<TEntity, bool>>(match, key.Parameters));
    }

    public Task<TEntity?> GetAsync(Expression<Func<TEntity, bool>> predicate, bool traking = false,
        params Expression<Func<TEntity, object>>[] includes)
    {
        IQueryable<TEntity> query = traking ? DbSet : DbSet.AsNoTracking();
        foreach (var include in includes ?? [])
            query = query.Include(include);
        return query.FirstOrDefaultAsync(predicate);
    }

    public virtual IQueryable<TEntity> GetAllQueryableAsync(bool tracking = false) =>
        tracking ? DbSet : DbSet.AsNoTracking();

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null) =>
        predicate == null ? DbSet.AnyAsync() : DbSet.AnyAsync(predicate);

    public Task<int> GetCountAsync(Expression<Func<TEntity, bool>>? predicate = null) =>
        predicate == null ? DbSet.CountAsync() : DbSet.CountAsync(predicate);

    public void Insert(TEntity entity) => DbSet.Add(entity);
    public void Insert(List<TEntity> entities) => DbSet.AddRange(entities);
    public void Update(TEntity entity) => DbSet.Update(entity);
    public void Delete(TEntity entity) => DbSet.Remove(entity);
    public Task SaveChangesAsync() => Context.SaveChangesAsync();
}
