using FluentValidation;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Queries.Reports.Position;

public sealed class PricePositionQueryValidator : AbstractValidator<PricePositionQuery>
{
    public PricePositionQueryValidator()
    {
        RuleFor(x => x).Must(x => PanelPagination.IsValid(x.Page, x.PageSize, 100))
            .WithMessage("شماره یا اندازهٔ صفحه معتبر نیست.");
        RuleFor(x => x.Status).Must(PricePosition.IsValidStatus)
            .WithMessage("وضعیت مقایسه معتبر نیست.");
        RuleFor(x => x.Search).MaximumLength(300)
            .WithMessage("عبارت جست‌وجو حداکثر ۳۰۰ کاراکتر است.");
    }
}
