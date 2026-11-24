using FluentValidation;

namespace PriceTracker.Application.Features.Commands.Brands.QueueDiscovery;

public sealed class BrandQueueDiscoveryCommandValidator : AbstractValidator<BrandQueueDiscoveryCommand>
{
    public BrandQueueDiscoveryCommandValidator()
    {
        RuleFor(x => x.BrandId).Must(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 40)
            .WithMessage("برند پیدا نشد.").WithState(_ => 404);
    }
}
