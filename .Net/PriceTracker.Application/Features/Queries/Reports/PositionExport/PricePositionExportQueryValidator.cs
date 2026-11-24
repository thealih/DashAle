using FluentValidation;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Queries.Reports.PositionExport;

public sealed class PricePositionExportQueryValidator : AbstractValidator<PricePositionExportQuery>
{
    public PricePositionExportQueryValidator()
    {
        RuleFor(x => x.Status).Must(PricePosition.IsValidStatus)
            .WithMessage("وضعیت مقایسه معتبر نیست.");
        RuleFor(x => x.Search).MaximumLength(300)
            .WithMessage("عبارت جست‌وجو حداکثر ۳۰۰ کاراکتر است.");
        RuleFor(x => x.Output).Must(x => x is { CanWrite: true, CanSeek: true })
            .WithMessage("خروجی گزارش قابل نوشتن نیست.");
        RuleFor(x => x.Limit).InclusiveBetween(1, 200_000)
            .WithMessage("سقف تعداد ردیف‌های گزارش معتبر نیست.");
    }
}
