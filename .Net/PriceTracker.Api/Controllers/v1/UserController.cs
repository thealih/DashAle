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
    /// <summary>ساخت حساب کاربری توسط مدیر</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserCommand? command, CancellationToken ct)
    {
        if (!ModelState.IsValid || command == null)
            return UnprocessableEntity(new { detail = "داده‌های درخواست معتبر نیستند." });

        return StatusCode(StatusCodes.Status201Created, await sender.Send(command, ct));
    }
}
