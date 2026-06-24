using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Infrastructure.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/issue-for-production")]
[Authorize]
public class IssueForProductionController(
    IssueForProductionService service,
    SapMasterDataService masterDataService,
    IPdfService pdfService) : ControllerBase
{
    [HttpPost("list")]
    public async Task<IActionResult> List([FromBody] PaginationRequest? request, CancellationToken cancellationToken)
    {
        var normalized = PaginationRequest.Normalize(request);
        var (items, totalCount) = await service.ListAsync(normalized, cancellationToken);
        return Ok(PaginationResponseFactory.Create(normalized, items, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await service.GetByIdAsync(id, cancellationToken);
        return item == null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Not found"))
            : Ok(ApiResponse<object>.Ok(item));
    }

    [HttpGet("{id:int}/order-lines")]
    public async Task<IActionResult> GetOrderLines(int id, CancellationToken cancellationToken)
    {
        var item = await service.GetByIdAsync(id, cancellationToken);
        if (item is null) return NotFound(ApiResponse<object>.Fail("SYS-02", "Not found"));
        var orderLines = ProductionRequestMapper.ParseOrderLines(item.RequestBody);
        return Ok(ApiResponse<object>.Ok(orderLines));
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> DownloadPdf(int id, CancellationToken cancellationToken)
    {
        var record = await service.GetByIdAsync(id, cancellationToken);
        if (record is null)
            return NotFound(ApiResponse<object>.Fail("SYS-02", "Record not found"));

        var orderLines = ProductionRequestMapper.ParseOrderLines(record.RequestBody);
        var order = orderLines?.ProductionOrder;
        var lines = orderLines?.ProductionOrderLinesEntryNumber ?? [];

        var itemCodes = lines
            .Select(x => x.ItemNo)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var itemDetailsMap = new Dictionary<string, ItemsResponse?>(StringComparer.OrdinalIgnoreCase);
        foreach (var itemCode in itemCodes)
        {
            itemDetailsMap[itemCode!] = await masterDataService.GetItemByCodeAsync(itemCode!, cancellationToken);
        }

        var itemsHtml = new StringBuilder();
        double totalQty = 0;
        double totalWeight = 0;

        foreach (var line in lines)
        {
            totalQty += line.IssuedQuantity;
            itemDetailsMap.TryGetValue(line.ItemNo ?? string.Empty, out var itemDetails);
            totalWeight += itemDetails?.InventoryWeight ?? 0;

            var freeText = string.IsNullOrWhiteSpace(line.FreeText) ? string.Empty : $" - {line.FreeText}";
            itemsHtml.Append($"""
                <tr>
                    <td>{(line.VisualOrder ?? 0) + 1}</td>
                    <td>{line.LineNumber}</td>
                    <td>{line.ItemNo}</td>
                    <td>{itemDetails?.ItemName}{freeText}</td>
                    <td class='center'>{line.IssuedQuantity}</td>
                    <td class='center'>{itemDetails?.InventoryUom}</td>
                    <td class='right'>{itemDetails?.InventoryWeight:F2}</td>
                </tr>
                """);
        }

        var placeholders = new Dictionary<string, string>
        {
            ["documentNo"] = record.Id.ToString(),
            ["documentDate"] = DateTime.Now.ToString("dd/MM/yyyy"),
            ["projectName"] = record.ProjectName ?? order?.ProjectName ?? string.Empty,
            ["projectNo"] = record.Project ?? order?.Project ?? string.Empty,
            ["drawingNo"] = order?.DrawingNo ?? string.Empty,
            ["productionNo"] = order?.DocumentNumber?.ToString() ?? string.Empty,
            ["productionDate"] = order?.CreationDate?.ToString("dd/MM/yyyy") ?? string.Empty,
            ["productNo"] = order?.ItemNumber ?? record.ItemNo ?? string.Empty,
            ["warehouse"] = lines.LastOrDefault()?.Warehouse ?? string.Empty,
            ["productName"] = order?.ProductDescription ?? record.ItemName ?? string.Empty,
            ["@items"] = itemsHtml.ToString(),
            ["totalQty"] = totalQty.ToString("F2"),
            ["totalWeight"] = totalWeight.ToString("F2"),
            ["journalRemarks"] = "Auto generated from system",
            ["userName"] = User.Identity?.Name ?? string.Empty,
        };

        var pdfBytes = await pdfService.GeneratePdfFromTemplateAsync(
            "issue-for-production-template.html", placeholders, cancellationToken);

        var fileName = $"IssueForProduction({record.Id}).pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SapInventoryGenExitRequestOrderLines orderLines, CancellationToken cancellationToken)
    {
        try
        {
            var entity = await service.SaveAsync(orderLines, null, cancellationToken);
            return Ok(ApiResponse<object>.Ok(entity));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail("VAL-01", ex.Message));
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SapInventoryGenExitRequestOrderLines orderLines, CancellationToken cancellationToken)
    {
        try
        {
            var entity = await service.SaveAsync(orderLines, id, cancellationToken);
            return Ok(ApiResponse<object>.Ok(entity));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<object>.Fail("SYS-02", "Not found"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.Fail("VAL-01", ex.Message));
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(id, cancellationToken);
        return deleted
            ? Ok(ApiResponse<object>.Ok(null, "Deleted"))
            : NotFound(ApiResponse<object>.Fail("SYS-02", "Not found"));
    }
}
