using System.Globalization;
using System.Net;
using System.Text;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Helpers;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

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

        var isServiceDoc = FormatDocType(order.DocType) == "Service";
        var lines = order.DocumentLines ?? [];
        var itemCodes = lines
            .Select(l => l.ItemCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var itemNameByItem = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var itemCode in itemCodes)
        {
            var item = await masterDataService.GetItemByCodeAsync(itemCode!, cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(item?.ItemName))
                itemNameByItem[itemCode!] = item.ItemName!;
        }

        var accountNameByCode = isServiceDoc
            ? await masterDataService.GetChartOfAccountNamesByCodesAsync(
                lines.Select(l => l.AccountCode ?? string.Empty).ToList(),
                cancellationToken)
            : [];

        var specialLines = (order.DocumentSpecialLines ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s.LineText))
            .ToList();
        var usedSpecialLines = new HashSet<SapDocumentSpecialLine>();

        var itemsHtml = new StringBuilder();
        var sr = 1;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var purchaseQty = FormatQtyWithUom(line.Quantity, line.UoMCode);
            var unitPrice = FormatPrice(currency, line.UnitPrice);
            var lineTotal = FormatMoney(currency, line.LineTotal ?? line.LineGrandTotal);
            itemNameByItem.TryGetValue(line.ItemCode ?? string.Empty, out var itemName);

            string partNo;
            string description;
            if (isServiceDoc && !string.IsNullOrWhiteSpace(line.AccountCode))
            {
                partNo = line.AccountCode!;
                accountNameByCode.TryGetValue(line.AccountCode!, out var accountName);
                description = string.IsNullOrWhiteSpace(accountName)
                    ? line.ItemDescription ?? string.Empty
                    : string.IsNullOrWhiteSpace(line.ItemDescription)
                        ? accountName
                        : $"{accountName} — {line.ItemDescription}";
            }
            else
            {
                partNo = line.ItemCode ?? string.Empty;
                description = string.IsNullOrWhiteSpace(line.ItemDescription)
                    ? itemName ?? string.Empty
                    : line.ItemDescription;
            }

            itemsHtml.Append($"""
                <tr>
                    <td class="center">{sr}</td>
                    <td class="part-no">{Escape(partNo)}</td>
                    <td>{Escape(description)}</td>
                    <td class="center">{Escape(FormatDate(deliveryFallback))}</td>
                    <td class="center">{Escape(purchaseQty)}</td>
                    <td class="right">{Escape(unitPrice)}</td>
                    <td class="right">{Escape(lineTotal)}</td>
                </tr>
                """);

            var after = line.LineNum ?? index;
            var matched = specialLines
                .Where(s => !usedSpecialLines.Contains(s)
                    && SapPurchaseOrderPayloadBuilder.SpecialLineFollows(s, after, index))
                .ToList();
            if (matched.Count > 0)
            {
                foreach (var special in matched)
                {
                    usedSpecialLines.Add(special);
                    itemsHtml.Append(BuildSpecialLineRow(special.LineText));
                }
            }
            else
            {
                itemsHtml.Append(BuildSpecialLineRow(line.FreeText));
            }

            sr++;
        }

        foreach (var leftover in specialLines.Where(s => !usedSpecialLines.Contains(s)))
            itemsHtml.Append(BuildSpecialLineRow(leftover.LineText));

        if (lines.Count == 0)
        {
            itemsHtml.Append("""
                <tr>
                    <td colspan="7" class="center">No line items</td>
                </tr>
                """);
        }

        var entityName = branch?.BplName ?? string.Empty;
        var terms = FormatTermsHtml(BuildTermsOfContract(order, entityName));
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
            ["@terms"] = terms,
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
            ["@documentSpecialLines"] = string.Empty,
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

        if (Constants.PoDispatchWarehouses.IsFactoryOrOffice(order.BPLId, warehouseCode))
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
            : dispatchTo ?? warehouseCode ?? string.Empty;

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

    private static readonly HashSet<string> BoldTermsHeadings = new(StringComparer.Ordinal)
    {
        "PAYMENT TERMS",
        "TERMS & CONDITIONS",
    };

    private static string FormatTermsHtml(string terms) =>
        string.Join("<br>",
            terms.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => BoldTermsHeadings.Contains(line) ? $"<b>{Escape(line)}</b>" : Escape(line)));

    private static string BuildSpecialLineRow(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var escaped = Escape(text.Trim())
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);
        return $"""
                <tr>
                    <td colspan="7" class="special-lines">Document Special Lines: {escaped}</td>
                </tr>
                """;
    }

    private static string FormatQtyWithUom(double? qty, string? uom = null)
    {
        if (qty is null) return string.Empty;
        var qtyText = SapDecimalPlaces.Format(qty.Value, SapDecimalPlaces.Quantities);
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
        return $"{currency} {amount.Value.ToString($"N{SapDecimalPlaces.Amounts}", CultureInfo.InvariantCulture)}";
    }

    private static string FormatPrice(string currency, double? price)
    {
        if (price is null) return string.Empty;
        return $"{currency} {price.Value.ToString($"N{SapDecimalPlaces.Prices}", CultureInfo.InvariantCulture)}";
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

    private static string BuildTermsOfContract(SapPurchaseOrdersResponse order, string entityName)
    {
        var lines = new List<string>();
        var paymentTerms = order.CreateUdfList()
            .Where(t => t.Id is >= 1 and <= 11)
            .Where(t => t.Basic is > 0 || t.Gst is > 0
                || !string.IsNullOrWhiteSpace(t.Desc)
                || !string.IsNullOrWhiteSpace(t.Stage)
                || !string.IsNullOrWhiteSpace(t.Type))
            .OrderBy(t => t.Id)
            .ToList();

        if (paymentTerms.Count > 0)
        {
            lines.Add("PAYMENT TERMS");
            for (var i = 0; i < paymentTerms.Count; i++)
                lines.Add($"{i + 1}. {paymentTerms[i].DropDownValue()}");
        }

        var priceBasis = TermValue(order.UPriceBasis);
        // No header Delivery Term on file: point the reader at the per-line Delivery Date column
        // instead of leaving a grammatically broken blank ("within . Delivery timelines...").
        var deliveryTerms = TermValue(order.UDelTerms);
        var deliveryTermsClause = deliveryTerms.Length > 0
            ? deliveryTerms
            : "the timeline mentioned above in the row level";
        var inspectionBy = TermValue(order.UInspectionBy);
        var packaging = TermValue(order.UTransportation);
        var branchName = string.IsNullOrWhiteSpace(entityName) ? "the Company" : entityName;
        lines.Add("TERMS & CONDITIONS");
        lines.Add($"1. Price Basis – Order is placed on {priceBasis} basis.");
        lines.Add("2. Scope & Quality – Supply/work shall strictly conform to PO specifications, drawings, and quality requirements. Any deviation requires prior written approval. Rejected material shall be replaced at supplier's cost.");
        lines.Add("3. Pricing – Prices are firm and fixed. No escalation shall be entertained unless specifically agreed in writing.");
        lines.Add("4. GST Compliance – GST shall be charged as applicable with correct GSTIN and HSN/SAC. ITC shall be availed only upon compliance with GST laws. Any loss of ITC, interest, penalty, or liability arising due to supplier's non-compliance shall be recoverable from the supplier.");
        lines.Add("5. TDS – TDS shall be deducted as per applicable statutory provisions. Exemption benefits, if any, shall be considered only upon submission of valid supporting documents.");
        lines.Add($"6. Delivery – All materials/services shall be delivered/completed within {deliveryTermsClause}. Delivery timelines are binding. Delay may result in penalty, cancellation, or procurement from alternate sources at supplier's risk and cost.");
        lines.Add($"7. Inspection – Materials/services shall be subject to inspection and approval by {inspectionBy}. Non-conforming supplies shall be rejected and replaced at supplier's cost.");
        lines.Add($"8. Packing & Delivery – Packaging shall be {packaging}. Adequate packing is mandatory. Any transit loss or damage due to improper packing shall be the supplier's responsibility. FOR deliveries shall be made strictly to the specified location.");
        lines.Add("9. Invoicing – Invoice shall mention PO No., GST details, HSN/SAC, item details, and applicable statutory particulars. Supporting documents (DC, LR, E-Way Bill, etc.) shall be submitted to purchase.pune@privilegeboilers.com with account@privilegeboilers.com in CC. Incomplete invoices shall not be processed.");
        lines.Add("10. Payment – Payment shall be made as per PO terms, subject to acceptance of material/services and compliance with contractual requirements. Advances, if any, shall be adjusted as per agreed terms. The Company reserves the right to withhold payment in case of disputes or non-compliance.");
        lines.Add("11. Warranty – Supplier warrants the material/work for 18 months from date of supply or 12 months from commissioning, whichever is earlier. Defects arising during the warranty period shall be rectified/replaced at supplier's cost.");
        lines.Add("12. Labour & Statutory Compliance – For Service/Job Work Orders, PF, ESIC and all other applicable labour/statutory compliances shall be the Contractor's responsibility.");
        lines.Add($"13. Site Safety & Insurance – Contractor shall be responsible for the safety, welfare and supervision of its personnel at site. {branchName} shall not be responsible for any accident, injury, death or damage involving Contractor's personnel, except where legally attributable to PBBPL. Valid WC/Employees Compensation Insurance Policy shall be submitted before commencement of work.");
        lines.Add("14. Confidentiality – All drawings, specifications, and information provided by the Company shall remain confidential and shall not be disclosed without prior written consent.");
        lines.Add("15. Termination – The Company reserves the right to terminate the PO for delay, breach, non-performance, or quality issues without liability except for accepted supplies/services.");
        lines.Add("16. Compliance – Supplier shall comply with all applicable laws including GST, Income Tax, Labour, Environmental, and Safety regulations.");
        lines.Add("17. Force Majeure – Delays caused by events beyond reasonable control shall be promptly notified to the Company.");
        lines.Add("18. Jurisdiction – All disputes shall be subject to the exclusive jurisdiction of courts at Pune, Maharashtra.");
        lines.Add("19. General – No subcontracting shall be permitted without prior written approval. The Company's decision regarding interpretation and execution of the PO shall be final and binding.");
        lines.Add("20. Indemnity – Supplier shall indemnify and hold harmless the Company against any loss, damage, claim, penalty, interest, or liability arising from breach of contract, statutory non-compliance, defective supply, or negligence on the part of the supplier.");
        return string.Join("\n", lines);
    }

    private static string TermValue(string? value) => (value ?? string.Empty).Trim();

    private static string Escape(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);
}
