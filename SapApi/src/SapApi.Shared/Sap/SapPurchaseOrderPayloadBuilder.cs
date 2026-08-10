using SapApi.Shared;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Shared.Sap;

/// <summary>
/// Builds a create/update PurchaseOrders body with only Service Layer–writable fields.
/// Calculated / read-only properties are stripped so SAP computes totals.
/// </summary>
public static class SapPurchaseOrderPayloadBuilder
{
    public static SapPurchaseOrdersResponse Prepare(SapPurchaseOrdersResponse source, bool isUpdate)
    {
        var docDate = source.DocDate ?? source.PostingDate ?? DateTime.UtcNow.Date;
        var docDue = source.DocDueDate ?? source.DueDate ?? docDate;
        var taxDate = source.TaxDate ?? docDate;

        var payload = new SapPurchaseOrdersResponse
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
            // U_Warehouse is not a valid PurchaseOrders UDF on this company DB.
            UWarehouse = null,
            UOwner = NullIfWhiteSpace(source.UOwner),
            UPoType = NullIfWhiteSpace(source.UPoType),
            UTrn = NullIfWhiteSpace(source.UTrn),
            UDisId = NullIfWhiteSpace(source.UDisId),
            UDispachAdd = NullIfWhiteSpace(source.UDispachAdd),
            URemark = NullIfWhiteSpace(source.URemark),
            // ADOC U_CardCode stores Dispatch To BP; never send invalid U_DispatchTo.
            UCardCode = NullIfWhiteSpace(source.UCardCode ?? source.UDispatchTo),
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
            DocumentLines = PrepareLines(source.DocumentLines, isUpdate, IsServiceDocument(source.DocType)),
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

        return payload;
    }

    /// <summary>
    /// GST payment % must live only on U_G11. Move any positive U_G1–U_G10 onto G11 and clear those slots.
    /// Also clears U_B11 (field does not exist on this company DB).
    /// </summary>
    internal static void NormalizePaymentTermGstToSlot11(SapPurchaseOrdersResponse payload)
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

    static List<SapInventoryTransferItemsRequests>? PrepareLines(
        List<SapInventoryTransferItemsRequests>? lines,
        bool isUpdate,
        bool isService)
    {
        if (lines is null || lines.Count == 0)
            return null;

        return lines
            .Where(l => isService
                ? !string.IsNullOrWhiteSpace(l.AccountCode)
                : !string.IsNullOrWhiteSpace(l.ItemCode))
            .Select((line, index) => isService
                ? new SapInventoryTransferItemsRequests
                {
                    LineNum = isUpdate ? line.LineNum ?? index : null,
                    ItemDescription = NullIfWhiteSpace(line.ItemDescription),
                    FreeText = NullIfWhiteSpace(line.FreeText),
                    AccountCode = NullIfWhiteSpace(line.AccountCode),
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountPercent = line.DiscountPercent,
                    TaxCode = NullIfWhiteSpace(line.TaxCode),
                    SACEntry = line.SACEntry,
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
                }
                : new SapInventoryTransferItemsRequests
                {
                    LineNum = isUpdate ? line.LineNum ?? index : null,
                    ItemCode = NullIfWhiteSpace(line.ItemCode),
                    ItemDescription = NullIfWhiteSpace(line.ItemDescription),
                    FreeText = NullIfWhiteSpace(line.FreeText),
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountPercent = line.DiscountPercent,
                    WarehouseCode = NullIfWhiteSpace(line.WarehouseCode),
                    TaxCode = NullIfWhiteSpace(line.TaxCode),
                    HSNEntry = line.HSNEntry,
                    SACEntry = line.SACEntry,
                    UoMCode = NullIfWhiteSpace(line.UoMCode),
                    UoMEntry = line.UoMEntry,
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
    }

    /// <summary>
    /// Returns ShipToCode only when it is not a Dispatch-To CardCode mistaken for an address name.
    /// </summary>
    static string? ResolveShipToCode(SapPurchaseOrdersResponse source)
    {
        var shipTo = NullIfWhiteSpace(source.ShipToCode);
        if (shipTo is null)
            return null;

        var dispatchBp = NullIfWhiteSpace(source.UCardCode) ?? NullIfWhiteSpace(source.UDispatchTo);
        if (dispatchBp is not null
            && string.Equals(shipTo, dispatchBp, StringComparison.OrdinalIgnoreCase))
            return null;

        var vendor = NullIfWhiteSpace(source.CardCode);
        if (vendor is not null
            && string.Equals(shipTo, vendor, StringComparison.OrdinalIgnoreCase))
            return null;

        return shipTo;
    }

    static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
