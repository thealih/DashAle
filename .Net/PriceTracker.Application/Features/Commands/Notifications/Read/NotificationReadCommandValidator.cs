using FluentValidation;

namespace PriceTracker.Application.Features.Commands.Notifications.Read;

public sealed class NotificationReadCommandValidator : AbstractValidator<NotificationReadCommand>
{
    public NotificationReadCommandValidator()
    {
        RuleFor(x => x.Ids).Must(ids => ids is null or { Length: <= 1000 })
            .WithMessage("حداکثر ۱۰۰۰ اعلان را هم‌زمان انتخاب کنید.");
    }
}
