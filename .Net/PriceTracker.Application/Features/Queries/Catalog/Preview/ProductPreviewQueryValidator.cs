using FluentValidation;

namespace PriceTracker.Application.Features.Queries.Catalog.Preview;

public sealed class ProductPreviewQueryValidator : AbstractValidator<ProductPreviewQuery>
{
    public ProductPreviewQueryValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0)
            .WithMessage("محصول پیدا نشد یا حذف شده است.").WithState(_ => 404);
    }
}
