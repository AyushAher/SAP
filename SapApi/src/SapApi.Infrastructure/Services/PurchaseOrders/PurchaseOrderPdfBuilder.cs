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

        var bplGst = (branch?.FederalTaxID ?? string.Empty).Trim();
        var bplPan = (branch?.PanNo ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(bplPan))
            bplPan = PanFromGstin(bplGst);

        var buyFrom = await BuildBuyFromAsync(order, cancellationToken);
        var shipTo = await BuildShipToAsync(order, bplGst, bplPan, cancellationToken);
        var currency = string.IsNullOrWhiteSpace(order.DocCurrency) ? "INR" : order.DocCurrency.Trim();
        var totalBasic = TotalBasic(order);
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

        var terms = BuildTermsOfContract();
        var entityName = branch?.BplName ?? string.Empty;
        var projectDisplay = FormatProject(order.Project, projectName);

        return new Dictionary<string, string>
        {
            ["bplName"] = Escape(entityName),
            ["bplAddr"] = Escape(branch?.Address ?? string.Empty),
            ["bplGst"] = Escape(bplGst),
            ["bplPan"] = Escape(bplPan),
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
            ["projectDisplay"] = Escape(projectDisplay),
            ["reference"] = Escape(order.NumAtCard ?? string.Empty),
            ["terms"] = Escape(terms),
            ["amountFigures"] = Escape(FormatMoney(currency, totalBasic)),
            ["amountWords"] = Escape(AmountInWords.ConvertToWords(totalBasic)),
            ["buyerName"] = Escape(buyerName ?? string.Empty),
            ["buyerEmail"] = Escape(buyerEmail ?? string.Empty),
            ["approverName"] = Escape(approverName ?? string.Empty),
            ["approverEmail"] = Escape(approverEmail ?? string.Empty),
            ["revNo"] = "-",
            ["entityName"] = Escape(entityName),
            ["userName"] = Escape(userName ?? string.Empty),
            ["printedOn"] = Escape(IndiaTime.FormatPrintedOn()),
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

    /// <summary>
    /// Ship To contact is the form Contact Person (U_SHIPTO). Address is the warehouse
    /// for Factory/Office (Store1/Store5) and the ship-to field (U_DispachAdd) for BP Loc.
    /// </summary>
    private async Task<PartyBlock> BuildShipToAsync(
        SapPurchaseOrdersResponse order,
        string branchGst,
        string branchPan,
        CancellationToken cancellationToken)
    {
        var contact = order.UShipTo ?? order.UContactPerson ?? string.Empty;
        var warehouseCode = ResolveDispatchWarehouse(order);

        if (Constants.PoDispatchWarehouses.IsFactoryOrOffice(warehouseCode))
        {
            var warehouse = await masterDataService.GetWarehouseByCodeAsync(
                warehouseCode, cancellationToken: cancellationToken);
            var gstin = NullIfWhiteSpace(warehouse?.FederalTaxID) ?? branchGst;
            var pan = string.IsNullOrWhiteSpace(PanFromGstin(gstin)) ? branchPan : PanFromGstin(gstin);
            return new PartyBlock
            {
                Name = warehouse?.WarehouseName ?? warehouseCode ?? string.Empty,
                Address = FormatWarehouseAddress(warehouse),
                Pin = warehouse?.ZipCode ?? string.Empty,
                State = warehouse?.State ?? string.Empty,
                StateCode = GstStateCode(gstin),
                Gst = gstin,
                Pan = pan,
                Contact = contact,
            };
        }

        var dispatchTo = order.DispatchToCardCode;
        var bp = await masterDataService.GetBusinessPartnerWithAddressesAsync(
            dispatchTo ?? string.Empty, cancellationToken);

        var name = !string.IsNullOrWhiteSpace(bp?.CardName)
            ? $"{bp!.CardCode} - {bp.CardName}"
            : dispatchTo ?? order.ShipToCode ?? warehouseCode ?? string.Empty;

        return BuildParty(
            name: name,
            address: order.UDispachAdd ?? string.Empty,
            source: PickAddress(bp, "Ship"),
            contact: contact);
    }

    private static string? ResolveDispatchWarehouse(SapPurchaseOrdersResponse order)
    {
        if (!string.IsNullOrWhiteSpace(order.UWarehouse))
            return order.UWarehouse.Trim();

        return (order.DocumentLines ?? [])
            .Select(l => l.WarehouseCode)
            .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))
            ?.Trim();
    }

    private static string FormatWarehouseAddress(WarehouseResponse? warehouse)
    {
        if (warehouse is null) return string.Empty;
        var parts = new[]
        {
            warehouse.StreetNo,
            warehouse.Street,
            warehouse.BuildingFloorRoom,
            warehouse.Block,
            warehouse.City,
        };
        return string.Join(", ",
            parts
                .Select(p => (p ?? string.Empty).Trim())
                .Where(p => p.Length > 0));
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    private static string FormatProject(string? code, string? name)
    {
        var trimmedCode = (code ?? string.Empty).Trim();
        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedCode.Length == 0) return trimmedName;
        if (trimmedName.Length == 0) return trimmedCode;
        return $"{trimmedCode} - {trimmedName}";
    }

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

    private static double TotalBasic(SapPurchaseOrdersResponse order)
    {
        var lineBasic = (order.DocumentLines ?? [])
            .Sum(l => l.LineTotal ?? l.RowTotalAfterDisc);
        if (lineBasic > 0)
            return lineBasic;

        return Math.Max(0, (order.DocTotal ?? 0) - (order.VatSum ?? 0));
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

    private static string BuildTermsOfContract() =>
        """
            STANDARD TERMS & CONDITIONS
            (To Be Printed on All Purchase Orders)
            1. Price Basis – Order is placed on ____ basis.
            2. Scope & Quality – Supply/work shall strictly conform to PO specifications, drawings, and quality requirements. Any deviation requires prior written approval. Rejected material shall be replaced at supplier's cost.
            3. Pricing – Prices are firm and fixed. No escalation shall be entertained unless specifically agreed in writing.
            4. GST Compliance – GST shall be charged as applicable with correct GSTIN and HSN/SAC. ITC shall be availed only upon compliance with GST laws. Any loss of ITC, interest, penalty, or liability arising due to supplier's non-compliance shall be recoverable from the supplier.
            5. TDS – TDS shall be deducted as per applicable statutory provisions. Exemption benefits, if any, shall be considered only upon submission of valid supporting documents.
            6. Delivery – All materials/services shall be delivered/completed within ____. Delivery timelines are binding. Delay may result in penalty, cancellation, or procurement from alternate sources at supplier's risk and cost.
            7. Inspection – Materials/services shall be subject to inspection and approval by ____. Non-conforming supplies shall be rejected and replaced at supplier's cost.
            8. Packing & Delivery – Packaging shall be ____. Adequate packing is mandatory. Any transit loss or damage due to improper packing shall be the supplier's responsibility. FOR deliveries shall be made strictly to the specified location.
            9. Invoicing – Invoice shall mention PO No., GST details, HSN/SAC, item details, and applicable statutory particulars. Supporting documents (DC, LR, E-Way Bill, etc.) shall be submitted to purchase.pune@privilegeboilers.com with account@privilegeboilers.com in CC. Incomplete invoices shall not be processed.
            10. Payment – Payment shall be made as per PO terms, subject to acceptance of material/services and compliance with contractual requirements. Advances, if any, shall be adjusted as per agreed terms. The Company reserves the right to withhold payment in case of disputes or non-compliance.
            11. Warranty – Supplier warrants the material/work for 18 months from date of supply or 12 months from commissioning, whichever is earlier. Defects arising during the warranty period shall be rectified/replaced at supplier's cost.
            12. Confidentiality – All drawings, specifications, and information provided by the Company shall remain confidential and shall not be disclosed without prior written consent.
            13. Termination – The Company reserves the right to terminate the PO for delay, breach, non-performance, or quality issues without liability except for accepted supplies/services.
            14. Compliance – Supplier shall comply with all applicable laws including GST, Income Tax, Labour, Environmental, and Safety regulations.
            15. Force Majeure – Delays caused by events beyond reasonable control shall be promptly notified to the Company.
            16. Jurisdiction – All disputes shall be subject to the exclusive jurisdiction of courts at Pune, Maharashtra.
            17. General – No subcontracting shall be permitted without prior written approval. The Company's decision regarding interpretation and execution of the PO shall be final and binding.
            18. Indemnity – Supplier shall indemnify and hold harmless the Company against any loss, damage, claim, penalty, interest, or liability arising from breach of contract, statutory non-compliance, defective supply, or negligence on the part of the supplier.
            """;

    private static string Escape(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);
}
