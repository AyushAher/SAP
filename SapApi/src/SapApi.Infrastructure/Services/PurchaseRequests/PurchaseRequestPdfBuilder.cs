using System.Globalization;
using System.Net;
using System.Text;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services.PurchaseRequests;

/// <summary>Builds the placeholder set for the Purchase Requisition print layout (matches the
/// WhatsApp-shared "01- Purchase Requisition" template).</summary>
public class PurchaseRequestPdfBuilder(SapMasterDataService masterDataService)
{
    public async Task<Dictionary<string, string>> BuildPlaceholdersAsync(
        SapPurchaseRequestsResponse pr,
        CancellationToken cancellationToken = default)
    {
        var branch = await masterDataService.GetBusinessPlaceByIdAsync(
            pr.BPLId,
            fields: ["BPLID", "BPLName", "Address"],
            cancellationToken: cancellationToken);

        var lines = pr.DocumentLines ?? [];
        var firstLine = lines.FirstOrDefault();

        string? preparedDesignation = null;
        if (pr.ReqType == Constants.SapPurchaseRequestReqType.Employee && int.TryParse(pr.Requester, out var employeeId))
        {
            var employee = await masterDataService.GetEmployeeByIdAsync(employeeId, cancellationToken);
            preparedDesignation = employee?.Position;
        }

        return new Dictionary<string, string>
        {
            ["entityName"] = Escape(branch?.BplName),
            ["entityAddress"] = Escape(branch?.Address),
            ["prNo"] = Escape(pr.DocNum?.ToString(CultureInfo.InvariantCulture) ?? pr.DocEntry?.ToString(CultureInfo.InvariantCulture)),
            ["postingDate"] = FormatDate(pr.DocDate),
            ["productNo"] = Escape(firstLine?.ItemCode),
            ["productDescription"] = Escape(firstLine?.ItemDescription),
            ["projectCode"] = Escape(pr.Project),
            ["dispatchAddress"] = Escape(pr.UDispachAdd),
            ["remarks"] = Escape(pr.Comments),
            ["rows"] = BuildRowsHtml(lines),
            ["preparedByName"] = Escape(pr.RequesterName),
            ["preparedByDesignation"] = Escape(preparedDesignation),
            ["preparedByEmail"] = Escape(pr.RequesterEmail),
            // No reliable link from a PurchaseRequest back to its ApprovalRequest chain today
            // (SupportingData is never set for PR approvals) — left blank rather than guessed.
            ["approvedByName"] = string.Empty,
            ["approvedByDesignation"] = string.Empty,
            ["approvedByEmail"] = string.Empty,
        };
    }

    private static string BuildRowsHtml(IReadOnlyList<SapInventoryTransferItemsRequests> lines)
    {
        var html = new StringBuilder();
        var sr = 1;
        foreach (var line in lines)
        {
            var freeText = string.IsNullOrWhiteSpace(line.FreeText)
                ? string.Empty
                : $"<div>{Escape(line.FreeText)}</div>";
            html.Append($"""
                <tr>
                    <td class="center">{sr}</td>
                    <td>
                        <div>{Escape(line.ItemCode)}</div>
                        <div>{Escape(line.ItemDescription)}</div>
                        {freeText}
                    </td>
                    <td class="right">{FormatQuantity(line.Quantity)}</td>
                    <td class="center">{Escape(line.UoMCode)}</td>
                </tr>
                """);
            sr++;
        }

        if (lines.Count == 0)
        {
            html.Append("""
                <tr>
                    <td colspan="4" class="center">No line items</td>
                </tr>
                """);
        }

        return html.ToString();
    }

    private static string FormatDate(DateTime? value) =>
        value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatQuantity(double? value) =>
        value is null ? string.Empty : value.Value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Escape(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);
}
