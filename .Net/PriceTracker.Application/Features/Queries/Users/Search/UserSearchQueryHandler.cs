using DNTPersianUtils.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Domain.Models.Enums;
using PriceTracker.Infrastracture;

namespace PriceTracker.Application.Features.Queries.Users.Search;

public sealed class UserSearchQueryHandler(AppDbContext db) : IRequestHandler<UserSearchQuery, UserSearchResult>
{
    public async Task<UserSearchResult> Handle(UserSearchQuery request, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var text = request.Search.Trim().ApplyCorrectYeKe();
            query = query.Where(x => x.UserName.Contains(text));
        }
        if (request.Role != null)
        {
            var role = UserRoleInput.Parse(request.Role);
            query = query.Where(x => x.Role == role);
        }
        if (request.Active is bool active)
            query = query.Where(x => x.IsActive == active);

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.UserName).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new CreatedUser(x.Id, x.UserName, x.Role == UserRole.Admin ? "admin" : "viewer", x.IsActive))
            .ToListAsync(ct);
        return new UserSearchResult(items, total, request.Page, request.PageSize);
    }
}
