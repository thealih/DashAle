using FluentValidation;

namespace PriceTracker.Application.Features.Queries.Reports.HistoryExport;

public sealed class HistoryExportQueryValidator : AbstractValidator<HistoryExportQuery>
{
    public HistoryExportQueryValidator()
    {
        RuleFor(x => x).Must(x => x.Start == null || x.End == null || x.Start < x.End)
            .WithMessage("پایان بازه باید بعد از شروع باشد.");
        RuleFor(x => x.Output).Must(x => x is { CanWrite: true, CanSeek: true })
            .WithMessage("خروجی گزارش قابل نوشتن نیست.");
        RuleFor(x => x.Limit).InclusiveBetween(1, 200_000)
            .WithMessage("سقف تعداد ردیف‌های گزارش معتبر نیست.");
    }
}
