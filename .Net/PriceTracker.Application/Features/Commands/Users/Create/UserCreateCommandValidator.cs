using DNTPersianUtils.Core;
using FluentValidation;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Commands.Users.Create;

public sealed class UserCreateCommandValidator : AbstractValidator<UserCreateCommand>
{
    public UserCreateCommandValidator()
    {
        RuleFor(x => x.Username).Must(value =>
        {
            var username = value?.Trim().ApplyCorrectYeKe();
            return !string.IsNullOrWhiteSpace(username) && username.Length <= 100 && !username.Any(char.IsControl);
        }).WithMessage("نام کاربری معتبر با حداکثر ۱۰۰ کاراکتر وارد کنید.");
        RuleFor(x => x.Password).Must(value => !string.IsNullOrWhiteSpace(value) && value.Length is >= 12 and <= 500)
            .WithMessage("رمز عبور باید ۱۲ تا ۵۰۰ کاراکتر داشته باشد.");
        RuleFor(x => x.Role).Must(value => value == null || UserRoleInput.IsValid(value))
            .WithMessage(UserRoleInput.InvalidRoleMessage);
    }
}
