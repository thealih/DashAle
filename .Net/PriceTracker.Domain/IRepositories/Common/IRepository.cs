using PriceTracker.Domain.Models.Entities.Common;

namespace PriceTracker.Domain.IRepositories.Common;

public interface IRepository<TEntity> : IKeyedRepository<TEntity, Guid> where TEntity : BaseEntity
{
    /// <summary>Stages an entity and returns its assigned key. Commit through the unit of work.</summary>
    Task<Guid> InsertAsync(TEntity entity);
    void SoftDelete(TEntity entity);
}
