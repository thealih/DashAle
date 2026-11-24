using FluentValidation;

namespace PriceTracker.Application.Features.Queries.Catalog.Detail;

public sealed class ProductDetailQueryValidator : AbstractValidator<ProductDetailQuery>
{
    public ProductDetailQueryValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0)
            .WithMessage("محصول پیدا نشد یا حذف شده است.").WithState(_ => 404);
    }
}
