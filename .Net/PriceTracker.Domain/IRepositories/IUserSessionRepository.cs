using PriceTracker.Domain.IRepositories.Common;
using PriceTracker.Domain.Models.Entities;

namespace PriceTracker.Domain.IRepositories;

public interface IUserSessionRepository : IKeyedRepository<UserSession, Guid>
{
}
