using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceTracker.Application.Features.Commands.Users.Create;
using PriceTracker.Application.Features.Queries.ExternalSample;
using PriceTracker.Application.Features.Queries.Users.GetAll;
using PriceTracker.Back.Models.Statics;

namespace PriceTracker.Api.Controllers.v1
{
    [Route("api/v{version:apiVersion}/users")]
    [ApiVersion("1.0")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UserController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// جستجو کاربران
        /// </summary>
        /// <returns></returns>
        [Authorize(Policy = nameof(ClientScopes.CkL1))]
        [HttpPost("search")]
        public async Task<IActionResult> SearchAsync(UserSearchQuery query) =>
            Ok(await _mediator.Send(query));

        /// <summary>
        /// افزودن کاربر جدید
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        //[Authorize(Policy = nameof(ClientScopes.CkL1))]
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateUserCommand command)=>
            Ok(await _mediator.Send(command));

    }
}
