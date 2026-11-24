using PriceTracker.Domain.Models.Enums;

namespace PriceTracker.Application.Features.Panel;

internal static class UserRoleInput
{
    internal const string InvalidRoleMessage = "نقش کاربر باید admin یا viewer باشد.";

    public static bool IsValid(string? input) => input is "admin" or "viewer";

    internal static UserRole Parse(string? input) => input switch
    {
        "admin" => UserRole.Admin,
        "viewer" => UserRole.Viewer,
        _ => throw new PanelException(422, InvalidRoleMessage)
    };
}
