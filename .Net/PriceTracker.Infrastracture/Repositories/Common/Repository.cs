using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using PriceTracker.Domain.Paging;
using PriceTracker.Domain.IRepositories.Common;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Domain.Models.Entities.Common;

namespace PriceTracker.Infrastracture.Repositories.Common
{
    public class Repository<EntityBase> : IRepository<EntityBase> where EntityBase : BaseEntity
    {
        protected readonly AppDbContext Context;
        protected readonly DbSet<EntityBase> DbSet;

        public Repository(AppDbContext context)
        {
            Context = context;
            DbSet = Context.Set<EntityBase>();
        }

        public Task<EntityBase?> GetByIdAsync(Guid id)
        {
            return DbSet.Where(x => !x.IsDeleted).FirstOrDefaultAsync(x => x.Id == id);
        }

        public Task<EntityBase> GetAsync(Expression<Func<EntityBase, bool>> predicate,
            bool traking = false,
            params Expression<Func<EntityBase, object>>[] includes)
        {
            var query = DbSet.Where(x => !x.IsDeleted).AsQueryable();

            if (!traking)
            {
                query = query.AsNoTracking();
            }

            if (includes is not null && includes.Any())
            {
                query = includes.Aggregate(query,
                          (current, include) => current.Include(include));
            }

            var result = query.FirstOrDefaultAsync(predicate);

            return result;
        }

        public IQueryable<EntityBase> GetAllQueryableAsync(bool tracking = false)
        {
            var query = DbSet.Where(x => !x.IsDeleted).AsQueryable();

            if (!tracking)
            {
                query = query.AsNoTracking();
            }

            query = query.OrderByDescending(x => x.CreatedAt);

            return query;
        }

        public Task<bool> AnyAsync(Expression<Func<EntityBase, bool>> predicate = null)
        {
            var result = DbSet.Where(x => !x.IsDeleted).AnyAsync(predicate);
            return result;
        }

        public Task<int> GetCountAsync(Expression<Func<EntityBase, bool>>? predicate = null)
        {
            var query = GetAllQueryableAsync();
            query = query.Where(predicate);
            return query.CountAsync();
        }

        public void Insert(EntityBase entity)
        {
            try
            {
                DbSet.AddAsync(entity);
            }
            catch (Exception e)
            {
                throw new Exception(typeof(EntityBase).Name, e);
            }
        }

        public void Insert(List<EntityBase> entities)
        {
            try
            {
                DbSet.AddRange(entities);
            }
            catch (Exception e)
            {
                throw new Exception(typeof(EntityBase).Name, e);
            }
        }

        public async Task<Guid> InsertAsync(EntityBase entity)
        {
            try
            {
                DbSet.AddAsync(entity);
                await SaveChangesAsync();
                return entity.Id;
            }
            catch (Exception e)
            {
                throw new Exception(typeof(EntityBase).Name, e);
            }
        }

        public void Update(EntityBase entity)
        {
            try
            {
                DbSet.Update(entity);
            }
            catch (Exception e)
            {
                throw new Exception(typeof(EntityBase).Name, e);
            }
        }

        public void SoftDelete(EntityBase entity)
        {
            entity.IsDeleted = true;
            DbSet.Remove(entity);
        }

        public void Delete(EntityBase entity)
        {
            DbSet.Remove(entity);
        }

        public Task SaveChangesAsync()
        {
            return Context.SaveChangesAsync();
        }


    }
}
