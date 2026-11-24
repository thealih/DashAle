using PriceTracker.Domain.Models.Enums;

namespace PriceTracker.Application.Features.Panel;

internal static class UserRoleInput
{
    internal static UserRole Parse(string? input) => input switch
    {
        "admin" => UserRole.Admin,
        "viewer" => UserRole.Viewer,
        _ => throw new PanelException(422, "نقش کاربر باید admin یا viewer باشد.")
    };
}
