using MediatR;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Queries.Users.Search;

public sealed record UserSearchResult(IReadOnlyList<CreatedUser> Items, int Total, int Page, int PageSize);

public sealed record UserSearchQuery(string? Search, string? Role, bool? Active, int Page, int PageSize)
    : IRequest<UserSearchResult>;
