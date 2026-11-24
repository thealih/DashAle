using FluentValidation;

namespace PriceTracker.Application.Features.Queries.Catalog.Timeline;

public sealed class ProductTimelineQueryValidator : AbstractValidator<ProductTimelineQuery>
{
    public ProductTimelineQueryValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0)
            .WithMessage("محصول پیدا نشد یا حذف شده است.").WithState(_ => 404);
    }
}
