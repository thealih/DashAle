using FluentValidation;

namespace PriceTracker.Application.Features.Commands.Catalog.QueueCheck;

public sealed class ProductQueueCheckCommandValidator : AbstractValidator<ProductQueueCheckCommand>
{
    public ProductQueueCheckCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0)
            .WithMessage("محصول پیدا نشد یا حذف شده است.").WithState(_ => 404);
    }
}
