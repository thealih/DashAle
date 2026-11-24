using PriceTracker.Domain.IRepositories;
using PriceTracker.Domain.Models.Entities;
using PriceTracker.Infrastracture.Repositories.Common;

namespace PriceTracker.Infrastracture.Repositories;

public sealed class LatestOfferRepository(AppDbContext context) : Repository<LatestOffer>(context), ILatestOfferRepository
{
}
