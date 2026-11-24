using FluentValidation;

namespace PriceTracker.Application.Features.Commands.Catalog.Update;

public sealed class ProductUpdateCommandValidator : AbstractValidator<ProductUpdateCommand>
{
    public ProductUpdateCommandValidator()
    {
        RuleFor(x => x.Ids).Must(ids => ids is { Length: >= 1 and <= 1000 } && ids.All(id => id > 0))
            .WithMessage("بین ۱ تا ۱۰۰۰ شناسهٔ معتبر محصول وارد کنید.");
        RuleFor(x => x.Action).IsInEnum().WithMessage("نوع عملیات محصول معتبر نیست.");
    }
}
