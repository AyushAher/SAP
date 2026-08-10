using System.Globalization;
using System.Net;
using System.Text;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Helpers;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services.PurchaseOrders;

public class PurchaseOrderPdfBuilder(SapMasterDataService masterDataService)
{
    public async Task<Dictionary<string, string>> BuildPlaceholdersAsync(
        SapPurchaseOrdersResponse order,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        var branch = await masterDataService.GetBusinessPlaceByIdAsync(
            order.BPLId,
            fields: ["BPLID", "BPLName", "Address", "FederalTaxID", "U_PANNO"],
            cancellationToken: cancellationToken);

        var projectName = await masterDataService.GetProjectNameAsync(order.Project, cancellationToken) ?? string.Empty;

        string? buyerName = null;
        string? buyerEmail = null;
        if (order.SalesPersonCode is int salesCode)
        {
            var salesPerson = await masterDataService.GetSalesPersonByCodeAsync(salesCode, cancellationToken);
            buyerName = salesPerson?.SalesEmployeeName;
        }

        string? approverName = null;
        if (order.DocumentsOwner is int ownerId)
        {
            var employee = await masterDataService.GetEmployeeByIdAsync(ownerId, cancellationToken);
            approverName = employee?.DisplayName;
        }

        var buyFrom = await BuildBuyFromAsync(order, cancellationToken);
        var shipTo = BuildShipTo(order);
        var currency = string.IsNullOrWhiteSpace(order.DocCurrency) ? "INR" : order.DocCurrency.Trim();
        var docTotal = order.DocTotal ?? 0;
        var deliveryFallback = order.DocDueDate ?? order.DueDate;

        var lines = order.DocumentLines ?? [];
        var itemCodes = lines
            .Select(l => l.ItemCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var inventoryUomByItem = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var itemCode in itemCodes)
        {
            var item = await masterDataService.GetItemByCodeAsync(itemCode!, cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(item?.InventoryUom))
                inventoryUomByItem[itemCode!] = item.InventoryUom!;
        }

        var itemsHtml = new StringBuilder();
        var sr = 1;
        foreach (var line in lines)
        {
            var description = BuildLineDescription(line);
            var purchaseQty = FormatQtyWithUom(line.Quantity, line.UoMCode);
            inventoryUomByItem.TryGetValue(line.ItemCode ?? string.Empty, out var inventoryUom);
            var stockQty = FormatQtyWithUom(line.InventoryQuantity ?? DeriveInventoryQty(line), inventoryUom);
            var unitPrice = FormatMoney(currency, line.UnitPrice);
            var lineTotal = FormatMoney(currency, line.LineTotal ?? line.LineGrandTotal);

            itemsHtml.Append($"""
                <tr>
                    <td class="center">{sr}</td>
                    <td>{Escape(line.ItemCode)}</td>
                    <td>{Escape(description)}</td>
                    <td class="center">{Escape(FormatDate(deliveryFallback))}</td>
                    <td class="center">{Escape(purchaseQty)}</td>
                    <td class="center">{Escape(stockQty)}</td>
                    <td class="right">{Escape(unitPrice)}</td>
                    <td class="right">{Escape(lineTotal)}</td>
                </tr>
                """);
            sr++;
        }

        if (lines.Count == 0)
        {
            itemsHtml.Append("""
                <tr>
                    <td colspan="8" class="center">No line items</td>
                </tr>
                """);
        }

        var terms = BuildTermsOfContract(order);
        var entityName = branch?.BplName ?? string.Empty;

        return new Dictionary<string, string>
        {
            ["bplName"] = Escape(entityName),
            ["bplAddr"] = Escape(branch?.Address ?? string.Empty),
            ["bplGst"] = Escape(branch?.FederalTaxID ?? string.Empty),
            ["bplPan"] = Escape(branch?.PanNo ?? string.Empty),
            ["bplEmail"] = "-",
            ["documentNo"] = Escape(order.DocNum?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            ["documentType"] = Escape(FormatDocType(order.DocType)),
            ["documentDate"] = Escape(FormatDate(order.DocDate)),
            ["poReference"] = Escape(Constants.PaymentRemarks.FormatPoNumber(order.BPLId, order.DocNum?.ToString())),
            ["buyFromVendor"] = Escape(buyFrom.Vendor),
            ["buyFromAddress"] = Escape(buyFrom.Address),
            ["buyFromPin"] = Escape(buyFrom.Pin),
            ["buyFromState"] = Escape(buyFrom.State),
            ["buyFromGst"] = Escape(buyFrom.Gst),
            ["buyFromPan"] = Escape(buyFrom.Pan),
            ["buyFromContact"] = Escape(buyFrom.Contact),
            ["shipToName"] = Escape(shipTo.Name),
            ["shipToAddress"] = Escape(shipTo.Address),
            ["shipToContact"] = Escape(shipTo.Contact),
            ["@items"] = itemsHtml.ToString(),
            ["projectNo"] = Escape(order.Project ?? string.Empty),
            ["projectName"] = Escape(projectName),
            ["reference"] = Escape(order.NumAtCard ?? string.Empty),
            ["terms"] = Escape(terms),
            ["amountFigures"] = Escape(FormatMoney(currency, docTotal)),
            ["amountWords"] = Escape(AmountInWords.ConvertToWords(docTotal)),
            ["buyerName"] = Escape(buyerName ?? string.Empty),
            ["buyerEmail"] = Escape(buyerEmail ?? string.Empty),
            ["approverName"] = Escape(approverName ?? string.Empty),
            ["entityName"] = Escape(entityName),
            ["userName"] = Escape(userName ?? string.Empty),
            ["printedOn"] = Escape(DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)),
            ["specialLines"] = Escape(order.Comments ?? string.Empty),
        };
    }

    private async Task<(string Vendor, string Address, string Pin, string State, string Gst, string Pan, string Contact)> BuildBuyFromAsync(
        SapPurchaseOrdersResponse order,
        CancellationToken cancellationToken)
    {
        var vendor = $"{order.CardCode} - {order.CardName}".Trim(' ', '-');
        var logistics = string.IsNullOrWhiteSpace(order.CardCode)
            ? null
            : await masterDataService.GetBusinessPartnerLogisticsAsync(order.CardCode!, cancellationToken);

        var billTo = logistics?.Addresses?
            .FirstOrDefault(a =>
                a.AddressType.Contains("Bill", StringComparison.OrdinalIgnoreCase)
                || string.Equals(a.AddressType, "boBillTo", StringComparison.OrdinalIgnoreCase)
                || string.Equals(a.AddressType, "bo_BillTo", StringComparison.OrdinalIgnoreCase))
            ?? logistics?.Addresses?.FirstOrDefault();

        var contact = logistics?.Contacts?
            .FirstOrDefault(c => order.ContactPersonCode is int code && c.InternalCode == code)
            ?? logistics?.Contacts?.FirstOrDefault();

        return (
            Vendor: vendor,
            Address: billTo?.FormattedAddress ?? string.Empty,
            Pin: string.Empty,
            State: string.Empty,
            Gst: string.Empty,
            Pan: string.Empty,
            Contact: contact?.Name ?? order.UContactPerson ?? string.Empty);
    }

    private static (string Name, string Address, string Contact) BuildShipTo(SapPurchaseOrdersResponse order)
    {
        var name = !string.IsNullOrWhiteSpace(order.UCardCode)
            ? order.UCardCode!
            : order.ShipToCode ?? order.UWarehouse ?? string.Empty;
        var address = order.UDispachAdd ?? string.Empty;
        var contact = order.UShipTo ?? order.UContactPerson ?? string.Empty;
        return (name, address, contact);
    }

    private static string BuildLineDescription(SapInventoryTransferItemsRequests line)
    {
        var description = line.ItemDescription ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(line.FreeText))
            description = string.IsNullOrWhiteSpace(description)
                ? line.FreeText!
                : $"{description} - {line.FreeText}";
        return description;
    }

    private static double? DeriveInventoryQty(SapInventoryTransferItemsRequests line)
    {
        if (line.Quantity is null) return null;
        var factor = line.UnitsOfMeasurment is > 0 ? line.UnitsOfMeasurment.Value : 1;
        return line.Quantity.Value * factor;
    }

    private static string FormatQtyWithUom(double? qty, string? uom = null)
    {
        if (qty is null) return string.Empty;
        var qtyText = qty.Value.ToString("0.##", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(uom) ? qtyText : $"{qtyText} {uom}";
    }

    private static string FormatMoney(string currency, double? amount)
    {
        if (amount is null) return string.Empty;
        return $"{currency} {amount.Value.ToString("N2", CultureInfo.InvariantCulture)}";
    }

    private static string FormatDate(DateTime? value) =>
        value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FormatDocType(string? docType) =>
        docType switch
        {
            "dDocument_Items" or "I" => "Item",
            "dDocument_Service" or "S" => "Service",
            _ => string.IsNullOrWhiteSpace(docType) ? "Purchase Order" : docType.Trim(),
        };

    private static string BuildTermsOfContract(SapPurchaseOrdersResponse order)
    {
        var parts = new List<string>();
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add($"{label}: {value.Trim()}");
        }

        Add("Delivery Terms", order.UDelTerms);
        Add("Inspection By", order.UInspectionBy);
        Add("Transportation", order.UTransportation);
        Add("Supervision", order.USupervision);
        Add("Transit Insurance", order.UTransitIns);
        Add("Drawings & Documents", order.UDrawDocs);
        Add("Loading", order.ULoading);
        Add("Unloading", order.UUnloading);
        Add("Warranty", order.UWarranty);
        Add("Painting", order.UPainting);
        Add("Test Certificates", order.UTestCerts);
        Add("Other Remarks", order.UOtherRemark);
        Add("Price Basis", order.UPriceBasis);

        return parts.Count == 0 ? (order.Comments ?? string.Empty) : string.Join(Environment.NewLine, parts);
    }

    private static string Escape(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);
}
