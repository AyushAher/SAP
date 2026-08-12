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
            buyerEmail = salesPerson?.Email;
        }

        string? approverName = null;
        string? approverEmail = null;
        if (order.DocumentsOwner is int ownerId)
        {
            var employee = await masterDataService.GetEmployeeByIdAsync(ownerId, cancellationToken);
            approverName = employee?.DisplayName;
            approverEmail = employee?.Email;
        }

        var buyFrom = await BuildBuyFromAsync(order, cancellationToken);
        var shipTo = await BuildShipToAsync(order, cancellationToken);
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
            var purchaseQty = FormatQtyWithUom(line.Quantity, line.UoMCode);
            inventoryUomByItem.TryGetValue(line.ItemCode ?? string.Empty, out var inventoryUom);
            var stockQty = FormatQtyWithUom(line.InventoryQuantity ?? DeriveInventoryQty(line), inventoryUom);
            var unitPrice = FormatMoney(currency, line.UnitPrice);
            var lineTotal = FormatMoney(currency, line.LineTotal ?? line.LineGrandTotal);

            itemsHtml.Append($"""
                <tr>
                    <td class="center">{sr}</td>
                    <td>{Escape(line.ItemCode)}</td>
                    <td>{Escape(line.ItemDescription)}</td>
                    <td class="center">{Escape(FormatDate(deliveryFallback))}</td>
                    <td class="center">{Escape(purchaseQty)}</td>
                    <td class="center">{Escape(stockQty)}</td>
                    <td class="right">{Escape(unitPrice)}</td>
                    <td class="right">{Escape(lineTotal)}</td>
                </tr>
                <tr>
                    <td colspan="8" class="special-lines">Document Special Lines{FormatSpecialLine(line.FreeText)}</td>
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
            ["buyFromVendor"] = Escape(buyFrom.Name),
            ["buyFromAddress"] = Escape(buyFrom.Address),
            ["buyFromPin"] = Escape(buyFrom.Pin),
            ["buyFromState"] = Escape(buyFrom.State),
            ["buyFromStateCode"] = Escape(buyFrom.StateCode),
            ["buyFromGst"] = Escape(buyFrom.Gst),
            ["buyFromPan"] = Escape(buyFrom.Pan),
            ["buyFromContact"] = Escape(buyFrom.Contact),
            ["shipToName"] = Escape(shipTo.Name),
            ["shipToAddress"] = Escape(shipTo.Address),
            ["shipToPin"] = Escape(shipTo.Pin),
            ["shipToState"] = Escape(shipTo.State),
            ["shipToStateCode"] = Escape(shipTo.StateCode),
            ["shipToGst"] = Escape(shipTo.Gst),
            ["shipToPan"] = Escape(shipTo.Pan),
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
            ["approverEmail"] = Escape(approverEmail ?? string.Empty),
            ["revNo"] = "-",
            ["entityName"] = Escape(entityName),
            ["userName"] = Escape(userName ?? string.Empty),
            ["printedOn"] = Escape(DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)),
            ["@documentSpecialLines"] = BuildDocumentSpecialLinesRow(order.Comments),
        };
    }

    private sealed record PartyBlock
    {
        public string Name { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string Pin { get; init; } = string.Empty;
        public string State { get; init; } = string.Empty;
        public string StateCode { get; init; } = string.Empty;
        public string Gst { get; init; } = string.Empty;
        public string Pan { get; init; } = string.Empty;
        public string Contact { get; init; } = string.Empty;
    }

    private async Task<PartyBlock> BuildBuyFromAsync(
        SapPurchaseOrdersResponse order,
        CancellationToken cancellationToken)
    {
        var bp = await masterDataService.GetBusinessPartnerWithAddressesAsync(
            order.CardCode ?? string.Empty, cancellationToken);
        var address = PickAddress(bp, "Bill");
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

        return BuildParty(
            name: $"{order.CardCode} - {order.CardName}".Trim(' ', '-'),
            address: billTo?.FormattedAddress ?? string.Empty,
            source: address,
            contact: contact?.Name ?? order.UContactPerson ?? string.Empty);
    }

    /// <summary>Ship To is the PO's Dispatch To partner (U_DisID) with its address UDF.</summary>
    private async Task<PartyBlock> BuildShipToAsync(
        SapPurchaseOrdersResponse order,
        CancellationToken cancellationToken)
    {
        var dispatchTo = order.DispatchToCardCode;
        var bp = await masterDataService.GetBusinessPartnerWithAddressesAsync(
            dispatchTo ?? string.Empty, cancellationToken);

        var name = !string.IsNullOrWhiteSpace(bp?.CardName)
            ? $"{bp!.CardCode} - {bp.CardName}"
            : dispatchTo ?? order.ShipToCode ?? order.UWarehouse ?? string.Empty;

        return BuildParty(
            name: name,
            address: order.UDispachAdd ?? string.Empty,
            source: PickAddress(bp, "Ship"),
            contact: order.UShipTo ?? order.UContactPerson ?? string.Empty);
    }

    private static PartyBlock BuildParty(string name, string address, SapBusinessPartnerAddress? source, string contact)
    {
        var gstin = (source?.Gstin ?? string.Empty).Trim();
        return new PartyBlock
        {
            Name = name,
            Address = address,
            Pin = source?.ZipCode ?? string.Empty,
            State = source?.State ?? string.Empty,
            StateCode = GstStateCode(gstin),
            Gst = gstin,
            Pan = PanFromGstin(gstin),
            Contact = contact,
        };
    }

    private static SapBusinessPartnerAddress? PickAddress(SapBusinessPartner? bp, string addressType)
    {
        var addresses = bp?.BPAddresses;
        if (addresses is null || addresses.Count == 0) return null;
        return addresses.FirstOrDefault(a =>
                (a.AddressType ?? string.Empty).Contains(addressType, StringComparison.OrdinalIgnoreCase))
            ?? addresses[0];
    }

    /// <summary>A 15-char GSTIN embeds the state code (chars 1-2) and the PAN (chars 3-12).</summary>
    private static string GstStateCode(string gstin) =>
        gstin.Length == 15 ? gstin[..2] : string.Empty;

    private static string PanFromGstin(string gstin) =>
        gstin.Length == 15 ? gstin.Substring(2, 10) : string.Empty;

    private static string FormatSpecialLine(string? freeText) =>
        string.IsNullOrWhiteSpace(freeText) ? string.Empty : $": {Escape(freeText.Trim())}";

    private static string BuildDocumentSpecialLinesRow(string? comments) =>
        string.IsNullOrWhiteSpace(comments)
            ? string.Empty
            : $"""
                <tr>
                    <td colspan="8">Document Special Lines: {Escape(comments.Trim())}</td>
                </tr>
                """;

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
