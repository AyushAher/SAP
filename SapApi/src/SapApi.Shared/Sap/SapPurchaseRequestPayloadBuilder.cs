using SapApi.Shared;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Shared.Sap;

/// <summary>
/// Builds a create/update PurchaseRequests body with only Service Layer–writable fields.
/// Calculated / read-only properties are stripped so SAP computes totals.
/// </summary>
public static class SapPurchaseRequestPayloadBuilder
{
    public static SapPurchaseRequestsResponse Prepare(SapPurchaseRequestsResponse source, bool isUpdate)
    {
        var docDate = source.DocDate ?? source.PostingDate ?? DateTime.UtcNow.Date;
        var docDue = source.DocDueDate ?? source.DueDate ?? docDate;
        var taxDate = source.TaxDate ?? docDate;
        var dispatchToBp = NullIfWhiteSpace(source.DispatchToCardCode);
        var isService = IsServiceDocument(source.DocType);
        var requiredDate = source.RequiredDate ?? docDue;
        var preparedLines = PrepareLines(source.DocumentLines, isUpdate, isService, requiredDate);

        var payload = new SapPurchaseRequestsResponse
        {
            CardCode = NullIfWhiteSpace(source.CardCode),
            CardName = NullIfWhiteSpace(source.CardName),
            NumAtCard = NullIfWhiteSpace(source.NumAtCard),
            Project = NullIfWhiteSpace(source.Project),
            Comments = NullIfWhiteSpace(source.Comments),
            DocDate = docDate,
            DocDueDate = docDue,
            TaxDate = taxDate,
            BPLId = source.BPLId,
            Series = source.Series,
            DocType = NullIfWhiteSpace(source.DocType),
            DocCurrency = NullIfWhiteSpace(source.DocCurrency),
            DocRate = source.DocRate,
            JournalMemo = NullIfWhiteSpace(source.JournalMemo),
            SalesPersonCode = source.SalesPersonCode,
            DocumentsOwner = source.DocumentsOwner,
            ContactPersonCode = source.ContactPersonCode,
            TransportationCode = source.TransportationCode,
            // ShipToCode must be a BPAddresses.AddressName on the document vendor — never a CardCode.
            // Dispatch-to BP is stored on U_CardCode; do not forward a mistaken CardCode as ShipToCode.
            ShipToCode = ResolveShipToCode(source),
            RoundingDiffAmount = source.RoundingDiffAmount,
            TotalDiscount = source.TotalDiscount,
            UStage = NullIfWhiteSpace(source.UStage),
            // U_Warehouse is not a valid PurchaseRequests UDF on this company DB.
            UWarehouse = null,
            UOwner = NullIfWhiteSpace(source.UOwner),
            UPoType = NullIfWhiteSpace(source.UPoType),
            UTrn = NullIfWhiteSpace(source.UTrn),
            // Dispatch To BP goes to U_DisID, matching what SAP itself writes on OPOR.
            UDisId = dispatchToBp,
            UDispachAdd = Truncate(NullIfWhiteSpace(source.UDispachAdd), 120),
            URemark = NullIfWhiteSpace(source.URemark),
            // U_CardCode is a read-only fallback for POs saved before the move — never written again.
            UCardCode = null,
            // Contact Person on the form is an employee (+ phone) stored in U_SHIPTO.
            UShipTo = NullIfWhiteSpace(source.UShipTo ?? source.UContactPerson),
            UContactPerson = null,
            UPriceBasis = NullIfWhiteSpace(source.UPriceBasis),
            UModeOfTransport = NullIfWhiteSpace(source.UModeOfTransport),
            UMatOutDoc = NullIfWhiteSpace(source.UMatOutDoc),
            UGoodsIssue = NullIfWhiteSpace(source.UGoodsIssue),
            UMatInDoc = NullIfWhiteSpace(source.UMatInDoc),
            UGoodsReceipt = NullIfWhiteSpace(source.UGoodsReceipt),
            UDelTerms = NullIfWhiteSpace(source.UDelTerms),
            UInspectionBy = NullIfWhiteSpace(source.UInspectionBy),
            UTransportation = NullIfWhiteSpace(source.UTransportation),
            USupervision = NullIfWhiteSpace(source.USupervision),
            UTransitIns = NullIfWhiteSpace(source.UTransitIns),
            UDrawDocs = NullIfWhiteSpace(source.UDrawDocs),
            ULoading = NullIfWhiteSpace(source.ULoading),
            UWarranty = NullIfWhiteSpace(source.UWarranty),
            UUnloading = NullIfWhiteSpace(source.UUnloading),
            UOtherRemark = NullIfWhiteSpace(source.UOtherRemark),
            UPainting = NullIfWhiteSpace(source.UPainting),
            UTestCerts = NullIfWhiteSpace(source.UTestCerts),
            UPackingForwarding = NullIfWhiteSpace(source.UPackingForwarding),
            UTcDispatchAddress = NullIfWhiteSpace(source.UTcDispatchAddress),
            // Fixed legal text for SAP; never taken from the client and never shown in the UI.
            UGstText = Constants.SapPurchaseOrderUdf.GstTextDefault,
            UTdsText = Constants.SapPurchaseOrderUdf.TdsTextDefault,
            UBasic1 = source.UBasic1,
            UBasic2 = source.UBasic2,
            UBasic3 = source.UBasic3,
            UBasic4 = source.UBasic4,
            UBasic5 = source.UBasic5,
            UBasic6 = source.UBasic6,
            UBasic7 = source.UBasic7,
            UBasic8 = source.UBasic8,
            UBasic9 = source.UBasic9,
            UBasic10 = source.UBasic10,
            UBasic11 = source.UBasic11,
            UGst1 = source.UGst1,
            UGst2 = source.UGst2,
            UGst3 = source.UGst3,
            UGst4 = source.UGst4,
            UGst5 = source.UGst5,
            UGst6 = source.UGst6,
            UGst7 = source.UGst7,
            UGst8 = source.UGst8,
            UGst9 = source.UGst9,
            UGst10 = source.UGst10,
            UGst11 = source.UGst11,
            UDes1 = NullIfWhiteSpace(source.UDes1),
            UDes2 = NullIfWhiteSpace(source.UDes2),
            UDes3 = NullIfWhiteSpace(source.UDes3),
            UDes4 = NullIfWhiteSpace(source.UDes4),
            UDes5 = NullIfWhiteSpace(source.UDes5),
            UDes6 = NullIfWhiteSpace(source.UDes6),
            UDes7 = NullIfWhiteSpace(source.UDes7),
            UDes8 = NullIfWhiteSpace(source.UDes8),
            UDes9 = NullIfWhiteSpace(source.UDes9),
            UDes10 = NullIfWhiteSpace(source.UDes10),
            UDes11 = NullIfWhiteSpace(source.UDes11),
            UStage1 = NullIfWhiteSpace(source.UStage1),
            UStage2 = NullIfWhiteSpace(source.UStage2),
            UStage3 = NullIfWhiteSpace(source.UStage3),
            UStage4 = NullIfWhiteSpace(source.UStage4),
            UStage5 = NullIfWhiteSpace(source.UStage5),
            UStage6 = NullIfWhiteSpace(source.UStage6),
            UStage7 = NullIfWhiteSpace(source.UStage7),
            UStage8 = NullIfWhiteSpace(source.UStage8),
            UStage9 = NullIfWhiteSpace(source.UStage9),
            UStage10 = NullIfWhiteSpace(source.UStage10),
            UStage11 = NullIfWhiteSpace(source.UStage11),
            UType1 = NullIfWhiteSpace(source.UType1),
            UType2 = NullIfWhiteSpace(source.UType2),
            UType3 = NullIfWhiteSpace(source.UType3),
            UType4 = NullIfWhiteSpace(source.UType4),
            UType5 = NullIfWhiteSpace(source.UType5),
            UType6 = NullIfWhiteSpace(source.UType6),
            UType7 = NullIfWhiteSpace(source.UType7),
            UType8 = NullIfWhiteSpace(source.UType8),
            UType9 = NullIfWhiteSpace(source.UType9),
            UType10 = NullIfWhiteSpace(source.UType10),
            UType11 = NullIfWhiteSpace(source.UType11),
            DocumentLines = preparedLines,
            DocumentSpecialLines = PrepareSpecialLines(
                source.DocumentLines, source.DocumentSpecialLines, preparedLines, isService),
            AdditionalUdf = PurchaseOrderOtherTermUdf.CopyWritableAdditionalUdf(source.AdditionalUdf),
            Requester = NullIfWhiteSpace(source.Requester),
            RequesterName = NullIfWhiteSpace(source.RequesterName),
            RequesterEmail = NullIfWhiteSpace(source.RequesterEmail),
            ReqCode = NullIfWhiteSpace(source.ReqCode),
            ReqType = source.ReqType ?? Constants.SapPurchaseRequestReqType.User,
            RequesterDepartment = source.RequesterDepartment,
            RequesterBranch = source.RequesterBranch,
            RequiredDate = requiredDate,
        };

        if (isUpdate)
            payload.DocEntry = source.DocEntry;

        // Never send calculated / read-only header totals — SAP computes them.
        payload.DocTotal = null;
        payload.VatSum = null;
        payload.DocNum = null;
        payload.DocumentStatus = null;
        payload.PostingDate = null;
        payload.DueDate = null;

        NormalizePaymentTermGstToSlot11(payload);
        StripPurchaseOrderOnlyFields(payload);
        NormalizeRequester(payload);

        return payload;
    }

    /// <summary>
    /// Drops UI-hidden GST/TDS remark UDFs so GET/create/update responses never hydrate them in the form.
    /// Call only on documents returned to the client — not on the Service Layer write payload.
    /// </summary>
    public static void OmitHiddenUdfDefaultsFromClientResponse(SapPurchaseRequestsResponse? document)
    {
        if (document is null)
            return;
        document.UGstText = null;
        document.UTdsText = null;
    }

    /// <summary>
    /// Local PO cache does not store POR12 text rows. Copy SAP DocumentSpecialLines (and line FreeText)
    /// onto the cached document so print/UI show the remarks that live in Service Layer.
    /// </summary>
    public static void MergeDocumentSpecialLinesFromSap(
        SapPurchaseRequestsResponse local,
        SapPurchaseRequestsResponse? sap)
    {
        if (sap is null)
            return;

        if (sap.DocumentSpecialLines is { Count: > 0 })
            local.DocumentSpecialLines = sap.DocumentSpecialLines;

        MergeOtherTermUdfFromSap(local, sap);

        if (local.DocumentLines is not { Count: > 0 })
            return;

        if (sap.DocumentLines is { Count: > 0 })
        {
            foreach (var line in local.DocumentLines)
            {
                if (!string.IsNullOrWhiteSpace(line.FreeText))
                    continue;
                var sapLine = sap.DocumentLines.FirstOrDefault(s => s.LineNum == line.LineNum);
                if (!string.IsNullOrWhiteSpace(sapLine?.FreeText))
                    line.FreeText = sapLine.FreeText;
            }
        }

        ApplySpecialLineTextOntoDocumentLines(local);
    }

    /// <summary>
    /// Local cache does not store Packing Forwarding / TC Dispatch Address. Copy those UDFs
    /// (and Unloading / Transportation / Transit Insurance) from the live SAP document.
    /// </summary>
    public static void MergeOtherTermUdfFromSap(
        SapPurchaseRequestsResponse local,
        SapPurchaseRequestsResponse sap)
    {
        if (!string.IsNullOrWhiteSpace(sap.UUnloading))
            local.UUnloading = sap.UUnloading;
        if (!string.IsNullOrWhiteSpace(sap.UTransportation))
            local.UTransportation = sap.UTransportation;
        if (!string.IsNullOrWhiteSpace(sap.UTransitIns))
            local.UTransitIns = sap.UTransitIns;
        if (!string.IsNullOrWhiteSpace(sap.UPackingForwarding))
            local.UPackingForwarding = sap.UPackingForwarding;
        if (!string.IsNullOrWhiteSpace(sap.UTcDispatchAddress))
            local.UTcDispatchAddress = sap.UTcDispatchAddress;

        local.AdditionalUdf = PurchaseOrderOtherTermUdf.MergeWritableAdditionalUdf(
            local.AdditionalUdf, sap.AdditionalUdf);
    }

    internal static void ApplySpecialLineTextOntoDocumentLines(SapPurchaseRequestsResponse document)
    {
        if (document.DocumentLines is not { Count: > 0 }
            || document.DocumentSpecialLines is not { Count: > 0 })
            return;

        for (var i = 0; i < document.DocumentLines.Count; i++)
        {
            var line = document.DocumentLines[i];
            if (!string.IsNullOrWhiteSpace(line.FreeText))
                continue;
            var after = line.LineNum ?? i;
            var text = document.DocumentSpecialLines
                .Where(s => SpecialLineFollows(s, after, i))
                .Select(s => s.LineText)
                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
            if (!string.IsNullOrWhiteSpace(text))
                line.FreeText = text.Trim();
        }
    }

    public static bool SpecialLineFollows(SapDocumentSpecialLine special, int lineNum, int index)
    {
        if (special.AfterLineNumber is int after)
            return after == lineNum || after == index;
        if (special.LineNum is int num)
            return num == lineNum || num == index;
        return false;
    }

    /// <summary>
    /// GST payment % must live only on U_G11. Move any positive U_G1–U_G10 onto G11 and clear those slots.
    /// Also clears U_B11 (field does not exist on this company DB).
    /// </summary>
    internal static void NormalizePaymentTermGstToSlot11(SapPurchaseRequestsResponse payload)
    {
        int? gstPercent = payload.UGst11 is > 0 ? payload.UGst11 : null;
        string? gstType = payload.UType11;
        string? gstStage = payload.UStage11;
        string? gstDesc = payload.UDes11;

        void TakeGstFromSlot(int? gst, string? type, string? stage, string? desc, int? basic, Action clearGstOnlyMeta)
        {
            if (gst is not > 0)
                return;

            if (gstPercent is null or 0)
            {
                gstPercent = gst;
                if (PaymentTermTypeOptions.IsGstMappedType(type) || string.IsNullOrWhiteSpace(gstType))
                    gstType = type;
                if (string.IsNullOrWhiteSpace(gstStage))
                    gstStage = stage;
                if (string.IsNullOrWhiteSpace(gstDesc))
                    gstDesc = desc;
            }

            // Legacy GST-only row on slots 1–10: drop type/stage/desc so it is not a ghost term.
            if (basic is not > 0)
                clearGstOnlyMeta();
        }

        TakeGstFromSlot(payload.UGst1, payload.UType1, payload.UStage1, payload.UDes1, payload.UBasic1,
            () => { payload.UType1 = null; payload.UStage1 = null; payload.UDes1 = null; });
        TakeGstFromSlot(payload.UGst2, payload.UType2, payload.UStage2, payload.UDes2, payload.UBasic2,
            () => { payload.UType2 = null; payload.UStage2 = null; payload.UDes2 = null; });
        TakeGstFromSlot(payload.UGst3, payload.UType3, payload.UStage3, payload.UDes3, payload.UBasic3,
            () => { payload.UType3 = null; payload.UStage3 = null; payload.UDes3 = null; });
        TakeGstFromSlot(payload.UGst4, payload.UType4, payload.UStage4, payload.UDes4, payload.UBasic4,
            () => { payload.UType4 = null; payload.UStage4 = null; payload.UDes4 = null; });
        TakeGstFromSlot(payload.UGst5, payload.UType5, payload.UStage5, payload.UDes5, payload.UBasic5,
            () => { payload.UType5 = null; payload.UStage5 = null; payload.UDes5 = null; });
        TakeGstFromSlot(payload.UGst6, payload.UType6, payload.UStage6, payload.UDes6, payload.UBasic6,
            () => { payload.UType6 = null; payload.UStage6 = null; payload.UDes6 = null; });
        TakeGstFromSlot(payload.UGst7, payload.UType7, payload.UStage7, payload.UDes7, payload.UBasic7,
            () => { payload.UType7 = null; payload.UStage7 = null; payload.UDes7 = null; });
        TakeGstFromSlot(payload.UGst8, payload.UType8, payload.UStage8, payload.UDes8, payload.UBasic8,
            () => { payload.UType8 = null; payload.UStage8 = null; payload.UDes8 = null; });
        TakeGstFromSlot(payload.UGst9, payload.UType9, payload.UStage9, payload.UDes9, payload.UBasic9,
            () => { payload.UType9 = null; payload.UStage9 = null; payload.UDes9 = null; });
        TakeGstFromSlot(payload.UGst10, payload.UType10, payload.UStage10, payload.UDes10, payload.UBasic10,
            () => { payload.UType10 = null; payload.UStage10 = null; payload.UDes10 = null; });

        payload.UGst1 = 0;
        payload.UGst2 = 0;
        payload.UGst3 = 0;
        payload.UGst4 = 0;
        payload.UGst5 = 0;
        payload.UGst6 = 0;
        payload.UGst7 = 0;
        payload.UGst8 = 0;
        payload.UGst9 = 0;
        payload.UGst10 = 0;
        payload.UGst11 = gstPercent ?? 0;
        payload.UBasic11 = null;

        if (!string.IsNullOrWhiteSpace(gstType))
            payload.UType11 = gstType;
        if (!string.IsNullOrWhiteSpace(gstStage))
            payload.UStage11 = gstStage;
        if (!string.IsNullOrWhiteSpace(gstDesc))
            payload.UDes11 = gstDesc;
    }

    static bool IsServiceDocument(string? docType) =>
        string.Equals(docType, Constants.PurchaseOrderDocType.Document_Service, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Copies LocationCode from any line that has it onto lines that do not.
    /// Service POs often send Loc. on only the last added row; SAP still requires it on every line.
    /// </summary>
    public static void CopyLocationCodeOntoLinesMissingIt(List<SapInventoryTransferItemsRequests>? lines)
    {
        if (lines is not { Count: > 0 })
            return;

        var location = lines.Select(l => l.LocationCode).FirstOrDefault(c => c is > 0);
        if (location is not > 0)
            return;

        foreach (var line in lines)
        {
            if (line.LocationCode is not > 0)
                line.LocationCode = location;
        }
    }

    /// <summary>
    /// SAP DocumentSpecialLines (dslt_Text) inserted after the item row whose FreeText they carry.
    /// Header special lines from the client are kept when line FreeText is empty.
    /// AfterLineNumber follows the LineNum assigned on the prepared document line.
    /// </summary>
    internal static List<SapDocumentSpecialLine>? PrepareSpecialLines(
        List<SapInventoryTransferItemsRequests>? lines,
        List<SapDocumentSpecialLine>? existing,
        List<SapInventoryTransferItemsRequests>? preparedLines,
        bool isService)
    {
        var fromLines = new List<SapDocumentSpecialLine>();
        if (lines is { Count: > 0 })
        {
            var preparedIndex = 0;
            foreach (var line in lines)
            {
                if (!IsIncludedLine(line, isService))
                    continue;

                var after = preparedLines is { Count: > 0 } && preparedIndex < preparedLines.Count
                    ? preparedLines[preparedIndex].LineNum ?? preparedIndex
                    : preparedIndex;
                preparedIndex++;

                var text = NullIfWhiteSpace(line.FreeText) ?? NullIfWhiteSpace(line.UFreeTxt);
                if (text is null)
                    continue;
                fromLines.Add(new SapDocumentSpecialLine
                {
                    AfterLineNumber = after,
                    LineType = "dslt_Text",
                    LineText = text,
                });
            }
        }

        if (fromLines.Count > 0)
            return fromLines;

        var fromHeader = existing?
            .Where(s => !string.IsNullOrWhiteSpace(s.LineText))
            .Select(s => new SapDocumentSpecialLine
            {
                AfterLineNumber = s.AfterLineNumber,
                LineType = NullIfWhiteSpace(s.LineType) ?? "dslt_Text",
                LineText = s.LineText!.Trim(),
            })
            .ToList();
        return fromHeader is { Count: > 0 } ? fromHeader : null;
    }

    static bool IsIncludedLine(SapInventoryTransferItemsRequests line, bool isService) =>
        isService
            ? !string.IsNullOrWhiteSpace(line.AccountCode)
            : !string.IsNullOrWhiteSpace(line.ItemCode);

    static List<SapInventoryTransferItemsRequests>? PrepareLines(
        List<SapInventoryTransferItemsRequests>? lines,
        bool isUpdate,
        bool isService,
        DateTime? fallbackRequiredDate)
    {
        if (lines is null || lines.Count == 0)
            return null;

        var prepared = lines
            .Where(l => IsIncludedLine(l, isService))
            .Select(line => isService
                ? new SapInventoryTransferItemsRequests
                {
                    LineNum = line.LineNum,
                    ItemDescription = NullIfWhiteSpace(line.ItemDescription),
                    FreeText = null,
                    UFreeTxt = null,
                    AccountCode = NullIfWhiteSpace(line.AccountCode),
                    Quantity = line.Quantity,
                    RequiredDate = line.RequiredDate ?? fallbackRequiredDate,
                    UnitPrice = line.UnitPrice,
                    DiscountPercent = line.DiscountPercent is > 0 ? line.DiscountPercent : null,
                    TaxCode = NullIfWhiteSpace(line.TaxCode),
                    SACEntry = line.SACEntry,
                    ProjectCode = NullIfWhiteSpace(line.ProjectCode),
                    // Service rows have no item/warehouse cost. Sending WarehouseCode or
                    // InventoryQuantity makes SAP reject GrossBuyPrice. LocationCode is still
                    // required by the company transaction notification.
                    LocationCode = line.LocationCode is > 0 ? line.LocationCode : null,
                    WarehouseCode = null,
                    ItemCode = null,
                    LineTotal = null,
                    TaxTotal = null,
                    GrossTotal = null,
                    InventoryQuantity = null,
                    UnitsOfMeasurment = null,
                    UseBaseUnits = null,
                    CostingCode = null,
                    CostingCode2 = null,
                    CostingCode3 = null,
                    CostingCode4 = null,
                    CostingCode5 = null,
                    UProdNo = null,
                    BaseType = line.BaseType,
                    BaseEntry = line.BaseEntry,
                    BaseLine = line.BaseLine,
                    WTLiable = NullIfWhiteSpace(line.WTLiable),
                    TaxLiable = NullIfWhiteSpace(line.TaxLiable),
                }
                : new SapInventoryTransferItemsRequests
                {
                    LineNum = line.LineNum,
                    ItemCode = NullIfWhiteSpace(line.ItemCode),
                    ItemDescription = NullIfWhiteSpace(line.ItemDescription),
                    // POR1.FreeTxt / U_FreeTxt are ~100 chars. Long remarks go on DocumentSpecialLines.
                    FreeText = null,
                    UFreeTxt = null,
                    Quantity = line.Quantity,
                    RequiredDate = line.RequiredDate ?? fallbackRequiredDate,
                    UnitPrice = line.UnitPrice,
                    DiscountPercent = line.DiscountPercent,
                    WarehouseCode = NullIfWhiteSpace(line.WarehouseCode),
                    LocationCode = line.LocationCode is > 0 ? line.LocationCode : null,
                    TaxCode = NullIfWhiteSpace(line.TaxCode),
                    HSNEntry = line.HSNEntry,
                    SACEntry = line.SACEntry,
                    AccountCode = NullIfWhiteSpace(line.AccountCode),
                    // MeasureUnit is not writable (ODBC -1029). SAP derives it from
                    // UseBaseUnits / UnitsOfMeasurment. AccountCode is sent for non-inventory
                    // items; inventory lines omit it from the client so G/L determination stays.
                    // UoMEntry -1 / UoMCode "Manual" is SAP's placeholder for items with no UoM
                    // group. Sending them on update makes Service Layer reject the line.
                    UoMCode = ResolveWritableUoMCode(line),
                    UoMEntry = ResolveWritableUoMEntry(line),
                    MeasureUnit = null,
                    UnitsOfMeasurment = line.UnitsOfMeasurment,
                    InventoryQuantity = line.InventoryQuantity
                        ?? (line.Quantity is > 0 && line.UnitsOfMeasurment is > 0
                            ? line.Quantity * line.UnitsOfMeasurment
                            : null),
                    UseBaseUnits = NullIfWhiteSpace(line.UseBaseUnits)
                        ?? (line.UnitsOfMeasurment is double per && Math.Abs(per - 1d) < 1e-9
                            ? Constants.SapBoolean.SapTrue
                            : line.UnitsOfMeasurment is not null
                                ? Constants.SapBoolean.SapFalse
                                : null),
                    ProjectCode = NullIfWhiteSpace(line.ProjectCode),
                    CostingCode = null,
                    CostingCode2 = null,
                    CostingCode3 = null,
                    CostingCode4 = null,
                    CostingCode5 = null,
                    UProdNo = null,
                    BaseType = line.BaseType,
                    BaseEntry = line.BaseEntry,
                    BaseLine = line.BaseLine,
                    WTLiable = NullIfWhiteSpace(line.WTLiable),
                    TaxLiable = NullIfWhiteSpace(line.TaxLiable),
                })
            .ToList();

        // Create (and create-approval) payloads need LineNum on every row. On update, a LineNum
        // that already exists is an in-place edit — inventing one for a new row makes SAP reject
        // the PUT. Leave missing LineNums null so Service Layer appends the row.
        if (!isUpdate)
            AssignMissingLineNums(prepared);
        // Item lines can belong to different warehouses/locations. Copying Loc. across rows
        // is only safe for service POs, which share one location.
        if (isService)
            CopyLocationCodeOntoLinesMissingIt(prepared);
        return prepared;
    }

    static void AssignMissingLineNums(List<SapInventoryTransferItemsRequests> lines)
    {
        var used = new HashSet<int>();
        foreach (var line in lines)
        {
            if (line.LineNum is >= 0)
                used.Add(line.LineNum.Value);
        }

        var next = 0;
        foreach (var line in lines)
        {
            if (line.LineNum is >= 0)
                continue;
            while (used.Contains(next))
                next++;
            line.LineNum = next;
            used.Add(next);
            next++;
        }
    }

    /// <summary>
    /// Returns ShipToCode only when it is not a Dispatch-To CardCode mistaken for an address name.
    /// </summary>
    static string? ResolveShipToCode(SapPurchaseRequestsResponse source)
    {
        var shipTo = NullIfWhiteSpace(source.ShipToCode);
        if (shipTo is null)
            return null;

        var dispatchBp = NullIfWhiteSpace(source.DispatchToCardCode);
        if (dispatchBp is not null
            && string.Equals(shipTo, dispatchBp, StringComparison.OrdinalIgnoreCase))
            return null;

        var vendor = NullIfWhiteSpace(source.CardCode);
        if (vendor is not null
            && string.Equals(shipTo, vendor, StringComparison.OrdinalIgnoreCase))
            return null;

        return shipTo;
    }

    /// <summary>
    /// SAP Manual UoM group rows use UoMEntry -1. That is not a writable group entry.
    /// </summary>
    static int? ResolveWritableUoMEntry(SapInventoryTransferItemsRequests line) =>
        line.UoMEntry is > 0 ? line.UoMEntry : null;

    static string? ResolveWritableUoMCode(SapInventoryTransferItemsRequests line)
    {
        if (ResolveWritableUoMEntry(line) is null)
            return null;
        var code = NullIfWhiteSpace(line.UoMCode);
        if (code is not null && code.Equals("Manual", StringComparison.OrdinalIgnoreCase))
            return null;
        return code;
    }

    /// <summary>
    /// OPRQ does not use PO logistics / payment / other-terms UDFs. Sending them (including U_G*=0)
    /// can make Service Layer look up valid values and fail with ODBC -2028.
    /// </summary>
    internal static void StripPurchaseOrderOnlyFields(SapPurchaseRequestsResponse payload)
    {
        payload.SalesPersonCode = null;
        payload.DocumentsOwner = null;
        payload.ContactPersonCode = null;
        payload.TransportationCode = null;
        payload.ShipToCode = null;
        payload.UStage = null;
        payload.UOwner = null;
        payload.UPoType = null;
        payload.UTrn = null;
        payload.UDisId = null;
        payload.UDispachAdd = null;
        payload.URemark = null;
        payload.UCardCode = null;
        payload.UShipTo = null;
        payload.UContactPerson = null;
        payload.UPriceBasis = null;
        payload.UModeOfTransport = null;
        payload.UMatOutDoc = null;
        payload.UGoodsIssue = null;
        payload.UMatInDoc = null;
        payload.UGoodsReceipt = null;
        payload.UDelTerms = null;
        payload.UInspectionBy = null;
        payload.UTransportation = null;
        payload.USupervision = null;
        payload.UTransitIns = null;
        payload.UDrawDocs = null;
        payload.ULoading = null;
        payload.UWarranty = null;
        payload.UUnloading = null;
        payload.UOtherRemark = null;
        payload.UPainting = null;
        payload.UTestCerts = null;
        payload.UPackingForwarding = null;
        payload.UTcDispatchAddress = null;
        payload.UGstText = null;
        payload.UTdsText = null;
        payload.UBasic1 = null;
        payload.UBasic2 = null;
        payload.UBasic3 = null;
        payload.UBasic4 = null;
        payload.UBasic5 = null;
        payload.UBasic6 = null;
        payload.UBasic7 = null;
        payload.UBasic8 = null;
        payload.UBasic9 = null;
        payload.UBasic10 = null;
        payload.UBasic11 = null;
        payload.UGst1 = null;
        payload.UGst2 = null;
        payload.UGst3 = null;
        payload.UGst4 = null;
        payload.UGst5 = null;
        payload.UGst6 = null;
        payload.UGst7 = null;
        payload.UGst8 = null;
        payload.UGst9 = null;
        payload.UGst10 = null;
        payload.UGst11 = null;
        payload.UDes1 = null;
        payload.UDes2 = null;
        payload.UDes3 = null;
        payload.UDes4 = null;
        payload.UDes5 = null;
        payload.UDes6 = null;
        payload.UDes7 = null;
        payload.UDes8 = null;
        payload.UDes9 = null;
        payload.UDes10 = null;
        payload.UDes11 = null;
        payload.UStage1 = null;
        payload.UStage2 = null;
        payload.UStage3 = null;
        payload.UStage4 = null;
        payload.UStage5 = null;
        payload.UStage6 = null;
        payload.UStage7 = null;
        payload.UStage8 = null;
        payload.UStage9 = null;
        payload.UStage10 = null;
        payload.UStage11 = null;
        payload.UType1 = null;
        payload.UType2 = null;
        payload.UType3 = null;
        payload.UType4 = null;
        payload.UType5 = null;
        payload.UType6 = null;
        payload.UType7 = null;
        payload.UType8 = null;
        payload.UType9 = null;
        payload.UType10 = null;
        payload.UType11 = null;
        payload.AdditionalUdf = null;
        payload.RequesterDepartment = null;
        payload.RequesterBranch = null;
    }

    /// <summary>
    /// ReqType 12 looks up OUSR.USER_CODE / USERID. A display name in Requester or ReqCode
    /// (e.g. "Aditya Aher") fails with ODBC -2028.
    /// </summary>
    internal static void NormalizeRequester(SapPurchaseRequestsResponse payload)
    {
        var requester = NullIfWhiteSpace(payload.Requester);
        var reqCode = NullIfWhiteSpace(payload.ReqCode);
        var name = NullIfWhiteSpace(payload.RequesterName);
        var isEmployee = payload.ReqType == Constants.SapPurchaseRequestReqType.Employee;

        if (!isEmployee && requester is not null && !LooksLikeSapUserCode(requester))
        {
            name ??= requester;
            requester = null;
        }

        if (reqCode is not null && !reqCode.All(char.IsDigit))
            reqCode = null;

        payload.Requester = requester;
        payload.RequesterName = name;
        payload.ReqCode = reqCode;
        if (requester is null && reqCode is null)
            payload.ReqType = null;
        else
            payload.ReqType ??= isEmployee
                ? Constants.SapPurchaseRequestReqType.Employee
                : Constants.SapPurchaseRequestReqType.User;
    }

    internal static bool LooksLikeSapUserCode(string value) =>
        value.Length is > 0 and <= 25
        && !value.Contains(' ', StringComparison.Ordinal)
        && !value.Contains('@', StringComparison.Ordinal);

    static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
