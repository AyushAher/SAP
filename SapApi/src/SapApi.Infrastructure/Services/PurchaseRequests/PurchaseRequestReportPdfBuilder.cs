using System.Globalization;
using System.Net;
using System.Text;
using SapApi.Shared.Models;
using SapApi.Shared.Responses;

namespace SapApi.Infrastructure.Services.PurchaseRequests;

public static class PurchaseRequestReportPdfBuilder
{
    public static Dictionary<string, string> BuildPlaceholders(
        List<PurchaseRequestReportLineResponse> rows,
        List<FilterModel> filters)
    {
        var rowsHtml = new StringBuilder();
        var sr = 1;
        foreach (var row in rows)
        {
            rowsHtml.Append($"""
                <tr>
                    <td class="center">{sr}</td>
                    <td class="center">{row.DocNum}</td>
                    <td class="center">{FormatDate(row.PostingDate)}</td>
                    <td>{Escape(row.UserName)}</td>
                    <td class="center">{FormatDate(row.RequiredDate)}</td>
                    <td>{Escape(row.ProjectCode)}</td>
                    <td class="center">{Escape(row.ItemType)}</td>
                    <td>{Escape(row.ItemCode)}</td>
                    <td>{Escape(row.Description)}</td>
                    <td>{Escape(row.FreeText)}</td>
                    <td class="right">{FormatQuantity(row.Quantity)}</td>
                    <td class="center">{Escape(row.Unit)}</td>
                </tr>
                """);
            sr++;
        }

        if (rows.Count == 0)
        {
            rowsHtml.Append("""
                <tr>
                    <td colspan="12" class="center">No purchase requests match the selected filters</td>
                </tr>
                """);
        }

        return new Dictionary<string, string>
        {
            ["rows"] = rowsHtml.ToString(),
            ["filterSummary"] = Escape(FormatFilterSummary(filters)),
        };
    }

    private static string FormatFilterSummary(List<FilterModel> filters)
    {
        if (filters.Count == 0)
            return "All purchase requests";

        var parts = filters
            .Where(f => f.Value is not null && !string.IsNullOrWhiteSpace(f.Value.ToString()))
            .Select(f => $"{f.Field}: {f.Value}");
        var summary = string.Join(" | ", parts);
        return string.IsNullOrWhiteSpace(summary) ? "All purchase requests" : summary;
    }

    private static string FormatDate(DateTime? value) =>
        value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatQuantity(double? value) =>
        value is null ? string.Empty : value.Value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Escape(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);
}
