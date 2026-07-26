using SapApi.Shared;
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
            DocType = NullIfWhiteSpace(source.DocType),
            DocCurrency = NullIfWhiteSpace(source.DocCurrency),
            DocRate = source.DocRate,
            JournalMemo = NullIfWhiteSpace(source.JournalMemo),
            SalesPersonCode = source.SalesPersonCode,
            DocumentsOwner = source.DocumentsOwner,
            ContactPersonCode = source.ContactPersonCode,
            TransportationCode = source.TransportationCode,
            ShipToCode = NullIfWhiteSpace(source.ShipToCode),
            RoundingDiffAmount = source.RoundingDiffAmount,
            TotalDiscount = source.TotalDiscount,
            UStage = NullIfWhiteSpace(source.UStage),
            UWarehouse = NullIfWhiteSpace(source.UWarehouse),
            UOwner = NullIfWhiteSpace(source.UOwner),
            UPoType = NullIfWhiteSpace(source.UPoType),
            UTrn = NullIfWhiteSpace(source.UTrn),
            UDisId = NullIfWhiteSpace(source.UDisId),
            UDispachAdd = NullIfWhiteSpace(source.UDispachAdd),
            URemark = NullIfWhiteSpace(source.URemark),
            UDispatchTo = NullIfWhiteSpace(source.UDispatchTo),
            UContactPerson = NullIfWhiteSpace(source.UContactPerson),
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

        return payload;
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
                    AccountCode = NullIfWhiteSpace(line.AccountCode),
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountPercent = line.DiscountPercent,
                    TaxCode = NullIfWhiteSpace(line.TaxCode),
                    SACEntry = line.SACEntry,
                    ProjectCode = NullIfWhiteSpace(line.ProjectCode),
                    CostingCode = NullIfWhiteSpace(line.CostingCode),
                    CostingCode2 = NullIfWhiteSpace(line.CostingCode2),
                    CostingCode3 = NullIfWhiteSpace(line.CostingCode3),
                    CostingCode4 = NullIfWhiteSpace(line.CostingCode4),
                    CostingCode5 = NullIfWhiteSpace(line.CostingCode5),
                    UProdNo = NullIfWhiteSpace(line.UProdNo),
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
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountPercent = line.DiscountPercent,
                    WarehouseCode = NullIfWhiteSpace(line.WarehouseCode),
                    TaxCode = NullIfWhiteSpace(line.TaxCode),
                    HSNEntry = line.HSNEntry,
                    SACEntry = line.SACEntry,
                    UoMCode = NullIfWhiteSpace(line.UoMCode),
                    UoMEntry = line.UoMEntry,
                    ProjectCode = NullIfWhiteSpace(line.ProjectCode),
                    CostingCode = NullIfWhiteSpace(line.CostingCode),
                    CostingCode2 = NullIfWhiteSpace(line.CostingCode2),
                    CostingCode3 = NullIfWhiteSpace(line.CostingCode3),
                    CostingCode4 = NullIfWhiteSpace(line.CostingCode4),
                    CostingCode5 = NullIfWhiteSpace(line.CostingCode5),
                    UProdNo = NullIfWhiteSpace(line.UProdNo),
                    BaseType = line.BaseType,
                    BaseEntry = line.BaseEntry,
                    BaseLine = line.BaseLine,
                    WTLiable = NullIfWhiteSpace(line.WTLiable),
                    TaxLiable = NullIfWhiteSpace(line.TaxLiable),
                })
            .ToList();
    }

    static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
