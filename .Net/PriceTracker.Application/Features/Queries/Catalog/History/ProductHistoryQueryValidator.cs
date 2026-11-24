using FluentValidation;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Queries.Catalog.History;

public sealed class ProductHistoryQueryValidator : AbstractValidator<ProductHistoryQuery>
{
    public ProductHistoryQueryValidator()
    {
        RuleFor(x => x).Must(x => PanelPagination.IsValid(x.Page, x.PageSize, 500))
            .WithMessage("شماره یا اندازهٔ صفحه معتبر نیست.");
        RuleFor(x => x.Id).GreaterThan(0)
            .WithMessage("محصول پیدا نشد یا حذف شده است.").WithState(_ => 404);
    }
}
