using FluentValidation;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Queries.Catalog.List;

public sealed class ProductListQueryValidator : AbstractValidator<ProductListQuery>
{
    public ProductListQueryValidator()
    {
        RuleFor(x => x).Must(x => PanelPagination.IsValid(x.Page, x.PageSize, 100))
            .WithMessage("شماره یا اندازهٔ صفحه معتبر نیست.");
    }
}
