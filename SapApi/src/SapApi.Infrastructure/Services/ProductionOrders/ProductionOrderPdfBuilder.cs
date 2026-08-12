using System.Globalization;
using System.Net;
using System.Text;
using SapApi.Shared;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Infrastructure.Services.ProductionOrders;

/// <summary>
/// Builds the printed production order from mirrored data only. Every value comes from the local
/// <c>ProductionOrders</c> / <c>ProductionOrderLines</c> tables (customer and project names are
/// resolved at sync time), so printing never contacts SAP.
/// </summary>
public class ProductionOrderPdfBuilder
{
    /// <summary>Shown instead of an empty cell so a missing value is obvious on the paper copy.</summary>
    private const string Dash = "-";

    public Dictionary<string, string> BuildPlaceholders(
        SapProductionOrdersResponse order,
        string? userName)
    {
        var lines = order.ProductionOrderLines ?? [];
        var itemsHtml = new StringBuilder();
        var totalPlanned = 0d;
        var totalIssued = 0d;
        var sr = 1;

        foreach (var line in lines)
        {
            totalPlanned += line.PlannedQuantity;
            totalIssued += line.IssuedQuantity;

            itemsHtml.Append($"""
                <tr>
                    <td class="center">{sr}</td>
                    <td>{Escape(line.ItemNo)}</td>
                    <td>{Escape(DescribeLine(line))}</td>
                    <td class="center">{Escape(FormatQty(line.PlannedQuantity))}</td>
                    <td class="center">{Escape(FormatQty(line.IssuedQuantity))}</td>
                    <td class="center">{Escape(Text(line.Warehouse))}</td>
                    <td class="center">{Escape(FormatLineUom(line.UoMCode))}</td>
                </tr>
                """);
            sr++;
        }

        if (lines.Count == 0)
        {
            itemsHtml.Append("""
                <tr>
                    <td colspan="7" class="center">No component lines</td>
                </tr>
                """);
        }

        return new Dictionary<string, string>
        {
            ["productionNo"] = Escape(Text(order.DocumentNumber ?? order.AbsoluteEntry)),
            ["status"] = Escape(Text(Constants.SapProductionOrderStatus.GetDisplay(order.Status))),
            ["orderDate"] = Escape(FormatDate(order.CreationDate ?? NullIfDefault(order.PostingDate))),
            ["productionCategory"] = Escape(Text(order.ProductionCategory)),
            ["startDate"] = Escape(FormatDate(order.StartDate)),
            ["dueDate"] = Escape(FormatDate(order.DueDate)),
            ["customerCode"] = Escape(Text(order.CustomerCode)),
            ["customerName"] = Escape(Text(order.CustomerName)),
            ["projectCode"] = Escape(Text(order.Project)),
            ["projectName"] = Escape(Text(order.ProjectName)),
            ["drawingNo"] = Escape(Text(order.DrawingNo)),
            ["salesOrderNo"] = Escape(Text(order.SalesOrderDocNum)),
            ["productNo"] = Escape(Text(order.ItemNumber)),
            ["productName"] = Escape(Text(order.ProductDescription)),
            ["plannedQty"] = Escape(FormatQty(order.PlannedQuantity)),
            ["uom"] = Escape(Text(order.InventoryUom)),
            ["completedQty"] = Escape(FormatQty(order.CompletedQuantity)),
            ["rejectedQty"] = Escape(FormatQty(order.RejectedQuantity)),
            ["receiptWarehouse"] = Escape(Text(order.Warehouse)),
            ["issueWarehouse"] = Escape(BuildIssueWarehouse(order)),
            ["@items"] = itemsHtml.ToString(),
            ["totalPlannedQty"] = Escape(FormatQty(totalPlanned)),
            ["totalIssuedQty"] = Escape(FormatQty(totalIssued)),
            ["remarks"] = Escape(Text(order.Remarks)),
            ["userName"] = Escape(Text(userName)),
            ["printedOn"] = Escape(DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)),
        };
    }

    /// <summary>
    /// SAP has no issue warehouse on the order header, so the components' warehouses are the
    /// answer; they are usually all the same store.
    /// </summary>
    private static string BuildIssueWarehouse(SapProductionOrdersResponse order)
    {
        if (!string.IsNullOrWhiteSpace(order.IssWarehouse))
            return order.IssWarehouse.Trim();

        var warehouses = (order.ProductionOrderLines ?? [])
            .Select(l => l.Warehouse)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return warehouses.Count == 0 ? Dash : string.Join(", ", warehouses);
    }

    private static string DescribeLine(SapProductionOrderLines line)
    {
        var description = string.IsNullOrWhiteSpace(line.ItemName) ? string.Empty : line.ItemName.Trim();
        var freeText = line.FreeText?.Trim();
        if (string.IsNullOrWhiteSpace(freeText))
            return string.IsNullOrEmpty(description) ? Dash : description;

        return string.IsNullOrEmpty(description) ? freeText : $"{description} - {freeText}";
    }

    /// <summary>
    /// WOR1 carries no unit-of-measure name: SAP returns UoMCode as a number and the mirror stores
    /// it as one, so a raw code is never printed as if it were a UoM.
    /// </summary>
    private static string FormatLineUom(object? uomCode)
    {
        if (SapProductionOrderUoMNormalizer.NormalizeUoMCode(uomCode) is not null)
            return Dash;

        return uomCode is string name && !string.IsNullOrWhiteSpace(name) ? name.Trim() : Dash;
    }

    private static DateTime? NullIfDefault(DateTime value) => value == default ? null : value;

    private static string FormatDate(DateTime? value) =>
        value is null ? Dash : value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string FormatQty(double value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Dash : value.Trim();

    private static string Text(int? value) =>
        value is null ? Dash : value.Value.ToString(CultureInfo.InvariantCulture);

    private static string Escape(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);
}
