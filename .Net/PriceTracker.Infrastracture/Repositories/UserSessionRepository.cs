using PriceTracker.Domain.IRepositories;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Infrastracture.Repositories.Common;

namespace PriceTracker.Infrastracture.Repositories;

public sealed class UserSessionRepository(AppDbContext context) : KeyedRepository<UserSession, Guid>(context, x => x.Id), IUserSessionRepository
{
}
