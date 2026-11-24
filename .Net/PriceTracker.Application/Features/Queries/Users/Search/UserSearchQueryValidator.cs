using FluentValidation;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Queries.Users.Search;

public sealed class UserSearchQueryValidator : AbstractValidator<UserSearchQuery>
{
    public UserSearchQueryValidator()
    {
        RuleFor(x => x).Must(x => PanelPagination.IsValid(x.Page, x.PageSize, 100) && (x.Search is null or { Length: <= 100 }))
            .WithMessage("عبارت جست‌وجو یا اطلاعات صفحه معتبر نیست.");
        RuleFor(x => x.Role).Must(value => value == null || UserRoleInput.IsValid(value))
            .WithMessage(UserRoleInput.InvalidRoleMessage);
    }
}
