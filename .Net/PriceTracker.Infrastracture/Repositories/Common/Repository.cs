using Microsoft.EntityFrameworkCore;
using PriceTracker.Domain.IRepositories.Common;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Domain.Models.Entities.Common;

namespace PriceTracker.Infrastracture.Repositories.Common;

public class Repository<TEntity>(AppDbContext context) : KeyedRepository<TEntity, Guid>(context, x => x.Id),
    IRepository<TEntity> where TEntity : BaseEntity
{
    public override IQueryable<TEntity> GetAllQueryableAsync(bool tracking = false) =>
        base.GetAllQueryableAsync(tracking).OrderByDescending(x => x.CreatedAt);

    public async Task<Guid> InsertAsync(TEntity entity)
    {
        await DbSet.AddAsync(entity);
        return entity.Id;
    }

    public void SoftDelete(TEntity entity)
    {
        if (entity.Id == Guid.Empty)
            throw new ArgumentException("Soft deletion requires an assigned entity key.", nameof(entity));

        var entry = Context.Entry(entity);
        if (entry.State == EntityState.Detached)
            DbSet.Attach(entity);

        if (entity is Product product)
            product.Archive(DateTime.UtcNow);
        else
            entity.IsDeleted = true;

        if (entry.State == EntityState.Added)
            return;

        entry.Property(x => x.IsDeleted).IsModified = true;
        if (entity is Product)
        {
            entry.Property(nameof(Product.DeletedAt)).IsModified = true;
            entry.Property(nameof(Product.IsTracked)).IsModified = true;
            entry.Property(nameof(Product.NextCheckAt)).IsModified = true;
            entry.Property(nameof(Product.LeaseUntil)).IsModified = true;
        }
    }
}
