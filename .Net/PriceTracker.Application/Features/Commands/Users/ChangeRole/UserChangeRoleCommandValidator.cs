using FluentValidation;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Application.Features.Commands.Users.ChangeRole;

public sealed class UserChangeRoleCommandValidator : AbstractValidator<UserChangeRoleCommand>
{
    public UserChangeRoleCommandValidator()
    {
        RuleFor(x => x.Role).Must(UserRoleInput.IsValid).WithMessage(UserRoleInput.InvalidRoleMessage);
        RuleFor(x => x.TargetId).NotEmpty().WithMessage("کاربر پیدا نشد.").WithState(_ => 404);
    }
}
