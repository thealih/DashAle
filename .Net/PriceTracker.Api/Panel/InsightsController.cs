using System.Text.Json.Serialization;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceTracker.Application.Features.Panel;
using PriceTracker.Infrastracture.Reports;

namespace PriceTracker.Api.Panel;

[ApiController, ApiVersionNeutral]
[Authorize(AuthenticationSchemes = PanelSecurity.Scheme)]
[Route("api")]
public sealed class InsightsController(ISender sender, IConfiguration configuration) : ControllerBase
{
    private const string XlsxMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const int MaxRows = 200_000;

    public sealed record ReadBody(long[]? Ids);
    public sealed record StoreBody([property: JsonPropertyName("seller_id")] string? SellerId,
        [property: JsonPropertyName("seller_name")] string? SellerName,
        [property: JsonPropertyName("undercut_toman")] int? UndercutToman);

    [HttpGet("notifications")]
    public async Task<IActionResult> Notices(CancellationToken ct,
        [FromQuery(Name = "brand_id")] string? brandId = null, [FromQuery(Name = "product_id")] long? productId = null,
        [FromQuery(Name = "seller_id")] string? sellerId = null, string? kind = null, bool unread = false,
        [FromQuery(Name = "alerts_only")] bool alertsOnly = false, string? start = null, string? end = null,
        int page = 1, [FromQuery(Name = "page_size")] int pageSize = 20)
    {
        CheckInput();
        return Reply(await sender.Send(new NoticeList(brandId, productId, sellerId, kind, unread, alertsOnly,
            PanelRequest.ParseDate(start), PanelRequest.ParseDate(end), page, pageSize), ct));
    }

    [HttpPost("notifications/read")]
    public async Task<IActionResult> Read(ReadBody? input, CancellationToken ct)
    {
        CheckInput();
        if (input == null)
            throw new PanelException(422, "بدنهٔ درخواست معتبر نیست.");
        await sender.Send(new ReadNotices(input.Ids, User.Identity?.Name), ct);
        return Reply(new { ok = true });
    }

    [HttpGet("sellers")]
    public async Task<IActionResult> Sellers(CancellationToken ct) => Reply(await sender.Send(new SellerList(), ct));

    [HttpGet("settings/own-store")]
    public async Task<IActionResult> Store(CancellationToken ct) => Reply(await sender.Send(new GetOwnStore(), ct));

    [HttpPut("settings/own-store"), Authorize(Policy = PanelSecurity.Admin)]
    public async Task<IActionResult> SaveStore(StoreBody? input, CancellationToken ct)
    {
        CheckInput();
        if (input == null)
            throw new PanelException(422, "بدنهٔ درخواست معتبر نیست.");
        return Reply(await sender.Send(new SetOwnStore(input.SellerId ?? "", input.SellerName ?? "",
            input.UndercutToman ?? 1), ct));
    }

    [HttpGet("price-position")]
    public async Task<IActionResult> Positions(CancellationToken ct, [FromQuery(Name = "brand_id")] string? brandId = null,
        string? q = null, string status = "not_best", int page = 1, [FromQuery(Name = "page_size")] int pageSize = 20)
    {
        CheckInput();
        return Reply(await sender.Send(new PositionQuery(brandId, q, status, page, pageSize), ct));
    }

    [HttpGet("price-position/excel")]
    public Task<IActionResult> PositionExcel(CancellationToken ct, [FromQuery(Name = "brand_id")] string? brandId = null,
        string? q = null, string status = "not_best")
    {
        CheckInput();
        return Export("price-position-live.xlsx",
            stream => new ExportPricePosition(stream, brandId, q, status, RowLimit()), ct);
    }

    [HttpGet("reports/excel")]
    public Task<IActionResult> ReportExcel(CancellationToken ct,
        [FromQuery(Name = "brand_id")] string? brandId = null, [FromQuery(Name = "product_id")] long? productId = null,
        [FromQuery(Name = "seller_id")] string? sellerId = null,
        string? start = null, string? end = null, [FromQuery(Name = "include_history")] bool includeHistory = true)
    {
        CheckInput();
        var from = PanelRequest.ParseDate(start);
        var to = PanelRequest.ParseDate(end);
        var now = XlsxWorkbook.Local(DateTime.UtcNow);
        return Export($"didban-live-{now:yyyyMMdd-HHmm}.xlsx",
            stream => new ExportHistory(stream, brandId, productId, sellerId, from, to, includeHistory, RowLimit()), ct);
    }

    private async Task<IActionResult> Export(string fileName, Func<Stream, IRequest> request, CancellationToken ct)
    {
        var path = Path.Combine(Path.GetTempPath(), $"price-tracker-{Guid.NewGuid():N}.xlsx");
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65_536,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try
        {
            await sender.Send(request(stream), ct);
            stream.Position = 0;
            return File(stream, XlsxMimeType, fileName);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    private int RowLimit() => Math.Clamp(configuration.GetValue("Reports:MaxRows", MaxRows), 1, MaxRows);

    private void CheckInput()
    {
        if (!ModelState.IsValid)
            throw new PanelException(422, "داده‌های درخواست معتبر نیستند.");
    }

    private static JsonResult Reply(object data) => new(data, PanelRequest.Json);
}
