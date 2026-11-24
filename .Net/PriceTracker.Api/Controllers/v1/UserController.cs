using PriceTracker.Application.Features.Commands.Users.Roles;
using PriceTracker.Application.Features.Queries.Users;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceTracker.Api.Panel;
using PriceTracker.Application.Features.Commands.Users.Create;

namespace PriceTracker.Api.Controllers.v1;

[ApiController, ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
[Authorize(Policy = PanelSecurity.Admin)]
public sealed class UserController(ISender sender) : ControllerBase
{
    public sealed record RoleBody(string? Role);

    [HttpGet]
    [HttpGet("search")]
    public async Task<IActionResult> Search(CancellationToken ct, string? q = null, string? role = null,
        bool? active = null, int page = 1, [FromQuery(Name = "page_size")] int pageSize = 20)
    {
        if (!ModelState.IsValid)
            return UnprocessableEntity(new { detail = "پارامترهای جست‌وجو معتبر نیستند." });

        return Ok(await sender.Send(new SearchUsers(q, role, active, page, pageSize), ct));
    }

    [HttpPatch("{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, RoleBody? input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input == null)
            return UnprocessableEntity(new { detail = "نقش کاربر را مشخص کنید." });
        if (!PanelSecurity.TrySessionIds(User, out var actorId, out var sessionId))
            return Unauthorized(new { detail = "برای ادامه وارد حساب کاربری شوید." });

        return Ok(await sender.Send(new ChangeUserRole(id, input.Role, actorId, sessionId), ct));
    }

    /// <summary>ساخت حساب کاربری توسط مدیر</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserCommand? command, CancellationToken ct)
    {
        if (!ModelState.IsValid || command == null)
            return UnprocessableEntity(new { detail = "داده‌های درخواست معتبر نیستند." });

        return StatusCode(StatusCodes.Status201Created, await sender.Send(command, ct));
    }
}
