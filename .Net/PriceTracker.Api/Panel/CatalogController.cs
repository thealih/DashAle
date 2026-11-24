using PriceTracker.Application.Features.Commands.Brands.QueueDiscovery;
using PriceTracker.Application.Features.Commands.Catalog.AddLink;
using PriceTracker.Application.Features.Commands.Catalog.QueueCheck;
using PriceTracker.Application.Features.Commands.Catalog.Update;
using PriceTracker.Application.Features.Queries.Brands.List;
using PriceTracker.Application.Features.Queries.Catalog.Detail;
using PriceTracker.Application.Features.Queries.Catalog.History;
using PriceTracker.Application.Features.Queries.Catalog.List;
using PriceTracker.Application.Features.Queries.Catalog.Preview;
using PriceTracker.Application.Features.Queries.Catalog.Timeline;
using PriceTracker.Application.Features.Queries.Dashboard.Summary;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceTracker.Application.Features.Panel;

namespace PriceTracker.Api.Panel;

[ApiController, ApiVersionNeutral]
[Authorize(AuthenticationSchemes = PanelSecurity.Scheme)]
[Route("api")]
public sealed class CatalogController(ISender sender) : ControllerBase
{
    public sealed record TrackingBody(long[]? Ids, bool? Tracked);
    public sealed record SelectionBody(long[]? Ids);
    public sealed record LinkBody([property: System.Text.Json.Serialization.JsonPropertyName("brand_id")] string? BrandId,
        string? Url);

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) =>
        Reply(await sender.Send(new DashboardSummaryQuery(), ct));

    [HttpGet("brands")]
    public async Task<IActionResult> Brands(CancellationToken ct) =>
        Reply(await sender.Send(new BrandListQuery(), ct));

    [HttpPost("brands/{brandId}/discover"), Authorize(Policy = PanelSecurity.Admin)]
    public async Task<IActionResult> Discover(string brandId, CancellationToken ct) =>
        Reply(new { job_id = await sender.Send(new BrandQueueDiscoveryCommand(brandId), ct),
            message = "جست‌وجو در صف قرار گرفت." }, 202);

    [HttpGet("products")]
    public async Task<IActionResult> Products(CancellationToken ct,
        [FromQuery(Name = "brand_id")] string? brandId = null, string? q = null, bool? tracked = null,
        bool deleted = false, int page = 1, [FromQuery(Name = "page_size")] int pageSize = 12)
    {
        CheckInput();
        if (deleted && !User.IsInRole("admin"))
            throw new PanelException(403, "مشاهدهٔ حذف‌شده‌ها به دسترسی مدیر نیاز دارد.");
        return Reply(await sender.Send(new ProductListQuery(brandId, q, tracked, deleted, page, pageSize), ct));
    }

    [HttpGet("products/{id:long}")]
    public async Task<IActionResult> Product(long id, CancellationToken ct) =>
        Reply(await sender.Send(new ProductDetailQuery(id), ct));

    [HttpGet("products/{id:long}/history")]
    public async Task<IActionResult> History(long id, CancellationToken ct,
        [FromQuery(Name = "seller_id")] string? sellerId = null, string? start = null, string? end = null,
        int page = 1, [FromQuery(Name = "page_size")] int pageSize = 100)
    {
        CheckInput();
        return Reply(await sender.Send(new ProductHistoryQuery(id, sellerId, PanelRequest.ParseDate(start), PanelRequest.ParseDate(end), page, pageSize), ct));
    }

    [HttpGet("products/{id:long}/timeline")]
    public async Task<IActionResult> Timeline(long id, CancellationToken ct,
        [FromQuery(Name = "seller_id")] string? sellerId = null) =>
        Reply(await sender.Send(new ProductTimelineQuery(id, sellerId), ct));

    [HttpGet("products/{id:long}/preview")]
    public async Task<IActionResult> Preview(long id, CancellationToken ct) =>
        Reply(await sender.Send(new ProductPreviewQuery(id), ct));

    [HttpPost("products/{id:long}/check"), Authorize(Policy = PanelSecurity.Admin)]
    public async Task<IActionResult> Check(long id, CancellationToken ct)
    {
        await sender.Send(new ProductQueueCheckCommand(id, User.Identity?.Name), ct);
        return Reply(new { message = "بررسی در صف قرار گرفت." }, 202);
    }

    [HttpPost("products/tracking"), Authorize(Policy = PanelSecurity.Admin)]
    public Task<IActionResult> Track(TrackingBody? body, CancellationToken ct)
    {
        CheckInput();
        if (body?.Tracked is not bool tracked)
            throw new PanelException(422, "وضعیت پایش را مشخص کنید.");
        return Update(body.Ids, tracked ? ProductAction.Track : ProductAction.Untrack, ct);
    }

    [HttpPost("products/delete"), Authorize(Policy = PanelSecurity.Admin)]
    public Task<IActionResult> Delete(SelectionBody? body, CancellationToken ct) =>
        Update(body?.Ids, ProductAction.Delete, ct);

    [HttpPost("products/restore"), Authorize(Policy = PanelSecurity.Admin)]
    public Task<IActionResult> Restore(SelectionBody? body, CancellationToken ct) =>
        Update(body?.Ids, ProductAction.Restore, ct);

    [HttpPost("products"), Authorize(Policy = PanelSecurity.Admin)]
    public async Task<IActionResult> Add(LinkBody? body, CancellationToken ct)
    {
        CheckInput();
        return Reply(await sender.Send(new ProductAddLinkCommand(body?.BrandId, body?.Url), ct), 201);
    }

    private async Task<IActionResult> Update(long[]? ids, ProductAction action, CancellationToken ct)
    {
        CheckInput();
        return Reply(new { updated = await sender.Send(new ProductUpdateCommand(ids ?? [], action, User.Identity?.Name), ct) });
    }

    private void CheckInput()
    {
        if (!ModelState.IsValid)
            throw new PanelException(422, "داده‌های درخواست معتبر نیستند.");
    }

    private static JsonResult Reply(object payload, int status = 200) =>
        new(payload, PanelRequest.Json) { StatusCode = status };
}
