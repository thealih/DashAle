using PriceTracker.Domain.IRepositories;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Infrastracture.Repositories.Common;

namespace PriceTracker.Infrastracture.Repositories;

public sealed class UserRefreshTokenRepository(AppDbContext context) : KeyedRepository<UserRefreshToken, string>(context, x => x.Hash), IUserRefreshTokenRepository
{
}
