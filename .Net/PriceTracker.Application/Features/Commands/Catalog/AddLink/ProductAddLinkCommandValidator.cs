using FluentValidation;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Commands.Catalog.AddLink;

public sealed class ProductAddLinkCommandValidator : AbstractValidator<ProductAddLinkCommand>
{
    public ProductAddLinkCommandValidator()
    {
        RuleFor(x => x.Url).Must(url => ProductLinks.TryGetSourceId(url, out _))
            .WithMessage(ProductLinks.InvalidUrlMessage);
        RuleFor(x => x.BrandId).Must(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 40)
            .WithMessage("برند پیدا نشد.").WithState(_ => 404);
    }
}
