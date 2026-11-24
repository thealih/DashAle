using FluentValidation;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Queries.Notifications.List;

public sealed class NotificationListQueryValidator : AbstractValidator<NotificationListQuery>
{
    public NotificationListQueryValidator()
    {
        RuleFor(x => x).Must(x => PanelPagination.IsValid(x.Page, x.PageSize, 100))
            .WithMessage("شماره یا اندازهٔ صفحه معتبر نیست.");
    }
}
