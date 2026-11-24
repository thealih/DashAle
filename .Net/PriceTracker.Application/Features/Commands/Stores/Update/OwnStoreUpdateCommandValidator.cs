using FluentValidation;
using System.Text.RegularExpressions;

namespace PriceTracker.Application.Features.Commands.Stores.Update;

public sealed partial class OwnStoreUpdateCommandValidator : AbstractValidator<OwnStoreUpdateCommand>
{
    public OwnStoreUpdateCommandValidator()
    {
        RuleFor(x => x).Must(x => x.SellerId is { Length: <= 100 } && SellerIdPattern().IsMatch(x.SellerId)
            && x.SellerName is { Length: <= 300 } && x.UndercutToman is >= 1 and <= 1_000_000_000)
            .WithMessage("تنظیمات فروشگاه پیش‌فرض معتبر نیست.");
    }

    [GeneratedRegex(@"\A[A-Za-z0-9_-]*\z")]
    private static partial Regex SellerIdPattern();
}
