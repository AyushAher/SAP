using SapApi.Domain.Entities;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services.PurchaseOrders;

public static class PurchaseOrderMapper
{
    public static void ApplyHeader(PurchaseOrder entity, SapPurchaseOrdersResponse sap, DateTime syncedAtUtc)
    {
        entity.IsDeleted = false;
        entity.DocEntry = sap.DocEntry ?? entity.DocEntry;
        entity.DocNum = sap.DocNum;
        entity.DocType = sap.DocType;
        entity.Project = sap.Project;
        entity.CardCode = sap.CardCode;
        entity.CardName = sap.CardName;
        entity.DocTotal = sap.DocTotal;
        entity.VatSum = sap.VatSum;
        entity.NumAtCard = sap.NumAtCard;
        entity.DocumentStatus = sap.DocumentStatus;
        entity.DocCurrency = sap.DocCurrency;
        entity.DocRate = sap.DocRate;
        entity.JournalMemo = sap.JournalMemo;
        entity.Comments = sap.Comments;
        entity.SalesPersonCode = sap.SalesPersonCode;
        entity.DocumentsOwner = sap.DocumentsOwner;
        entity.TransportationCode = sap.TransportationCode;
        entity.DocDate = sap.DocDate ?? sap.PostingDate;
        entity.DocDueDate = sap.DocDueDate ?? sap.DueDate;
        entity.TaxDate = sap.TaxDate;
        entity.BPLId = sap.BPLId;
        entity.ContactPersonCode = sap.ContactPersonCode;
        entity.ShipToCode = sap.ShipToCode;
        entity.RoundingDiffAmount = sap.RoundingDiffAmount;
        entity.TotalDiscount = sap.TotalDiscount;
        entity.UStage = sap.UStage;
        entity.UWarehouse = sap.UWarehouse;
        entity.UOwner = sap.UOwner;
        entity.UPoType = sap.UPoType;
        entity.UTrn = sap.UTrn;
        entity.UDisId = sap.UDisId;
        entity.UDispachAdd = sap.UDispachAdd;
        entity.URemark = sap.URemark;
        entity.UDispatchTo = sap.UDispatchTo;
        entity.UContactPerson = sap.UContactPerson;
        entity.UPriceBasis = sap.UPriceBasis;
        entity.UModeOfTransport = sap.UModeOfTransport;
        entity.UMatOutDoc = sap.UMatOutDoc;
        entity.UGoodsIssue = sap.UGoodsIssue;
        entity.UMatInDoc = sap.UMatInDoc;
        entity.UGoodsReceipt = sap.UGoodsReceipt;
        entity.UDelTerms = sap.UDelTerms;
        entity.UInspectionBy = sap.UInspectionBy;
        entity.UTransportation = sap.UTransportation;
        entity.USupervision = sap.USupervision;
        entity.UTransitIns = sap.UTransitIns;
        entity.UDrawDocs = sap.UDrawDocs;
        entity.ULoading = sap.ULoading;
        entity.UWarranty = sap.UWarranty;
        entity.UUnloading = sap.UUnloading;
        entity.UOtherRemark = sap.UOtherRemark;
        entity.UPainting = sap.UPainting;
        entity.UTestCerts = sap.UTestCerts;
        entity.SyncedAtUtc = syncedAtUtc;
        entity.LastModifiedOn = syncedAtUtc;
    }

    public static List<PurchaseOrderLine> MapLines(int purchaseOrderId, IEnumerable<SapInventoryTransferItemsRequests>? lines)
    {
        if (lines is null)
            return [];

        return lines.Select((line, index) => new PurchaseOrderLine
        {
            PurchaseOrderId = purchaseOrderId,
            LineNum = line.LineNum ?? index,
            ItemCode = line.ItemCode,
            ItemDescription = line.ItemDescription,
            AccountCode = line.AccountCode,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            DiscountPercent = line.DiscountPercent,
            LineTotal = line.LineTotal,
            TaxPercentagePerRow = line.TaxPercentagePerRow,
            TaxTotal = line.TaxTotal,
            TaxCode = line.TaxCode,
            WTLiable = line.WTLiable,
            TaxLiable = line.TaxLiable,
            GrossTotal = line.GrossTotal,
            WarehouseCode = line.WarehouseCode,
            HSNEntry = line.HSNEntry,
            SACEntry = line.SACEntry,
            UoMCode = line.UoMCode,
            UoMEntry = line.UoMEntry,
            UnitsOfMeasurment = line.UnitsOfMeasurment,
            InventoryQuantity = line.InventoryQuantity,
            UseBaseUnits = line.UseBaseUnits,
            ProjectCode = line.ProjectCode,
            CostingCode = line.CostingCode,
            CostingCode2 = line.CostingCode2,
            CostingCode3 = line.CostingCode3,
            CostingCode4 = line.CostingCode4,
            CostingCode5 = line.CostingCode5,
            UProdNo = line.UProdNo,
            BaseType = TryToInt(line.BaseType),
            BaseEntry = line.BaseEntry,
            BaseLine = line.BaseLine,
        }).ToList();
    }

    public static List<PurchaseOrderPaymentTerm> MapPaymentTerms(int purchaseOrderId, SapPurchaseOrdersResponse sap)
    {
        return sap.CreateUdfList()
            .Where(t => t.Id is >= 1 and <= 11)
            .Where(t => t.Basic is not null || t.Gst is not null
                || !string.IsNullOrWhiteSpace(t.Desc)
                || !string.IsNullOrWhiteSpace(t.Stage)
                || !string.IsNullOrWhiteSpace(t.Type))
            .Select(t => new PurchaseOrderPaymentTerm
            {
                PurchaseOrderId = purchaseOrderId,
                Slot = t.Id!.Value,
                Basic = t.Basic is null ? null : Convert.ToInt32(t.Basic.Value),
                Gst = t.Gst is null ? null : Convert.ToInt32(t.Gst.Value),
                Description = t.Desc,
                Stage = t.Stage,
                Type = t.Type,
            })
            .ToList();
    }

    public static SapPurchaseOrdersResponse ToSapResponse(PurchaseOrder entity, bool includeLines)
    {
        var response = new SapPurchaseOrdersResponse
        {
            DocEntry = entity.DocEntry,
            DocNum = entity.DocNum,
            DocType = entity.DocType,
            Project = entity.Project,
            CardCode = entity.CardCode,
            CardName = entity.CardName,
            DocTotal = entity.DocTotal,
            VatSum = entity.VatSum,
            NumAtCard = entity.NumAtCard,
            DocumentStatus = entity.DocumentStatus,
            DocCurrency = entity.DocCurrency,
            DocRate = entity.DocRate,
            JournalMemo = entity.JournalMemo,
            Comments = entity.Comments,
            SalesPersonCode = entity.SalesPersonCode,
            DocumentsOwner = entity.DocumentsOwner,
            TransportationCode = entity.TransportationCode,
            DocDate = entity.DocDate,
            PostingDate = entity.DocDate,
            DueDate = entity.DocDueDate,
            DocDueDate = entity.DocDueDate,
            TaxDate = entity.TaxDate,
            BPLId = entity.BPLId,
            ContactPersonCode = entity.ContactPersonCode,
            ShipToCode = entity.ShipToCode,
            RoundingDiffAmount = entity.RoundingDiffAmount,
            TotalDiscount = entity.TotalDiscount,
            UStage = entity.UStage,
            UWarehouse = entity.UWarehouse,
            UOwner = entity.UOwner,
            UPoType = entity.UPoType,
            UTrn = entity.UTrn,
            UDisId = entity.UDisId,
            UDispachAdd = entity.UDispachAdd,
            URemark = entity.URemark,
            UDispatchTo = entity.UDispatchTo,
            UContactPerson = entity.UContactPerson,
            UPriceBasis = entity.UPriceBasis,
            UModeOfTransport = entity.UModeOfTransport,
            UMatOutDoc = entity.UMatOutDoc,
            UGoodsIssue = entity.UGoodsIssue,
            UMatInDoc = entity.UMatInDoc,
            UGoodsReceipt = entity.UGoodsReceipt,
            UDelTerms = entity.UDelTerms,
            UInspectionBy = entity.UInspectionBy,
            UTransportation = entity.UTransportation,
            USupervision = entity.USupervision,
            UTransitIns = entity.UTransitIns,
            UDrawDocs = entity.UDrawDocs,
            ULoading = entity.ULoading,
            UWarranty = entity.UWarranty,
            UUnloading = entity.UUnloading,
            UOtherRemark = entity.UOtherRemark,
            UPainting = entity.UPainting,
            UTestCerts = entity.UTestCerts,
        };

        ApplyPaymentTermSlots(response, entity.PaymentTerms);

        if (includeLines)
        {
            response.DocumentLines = entity.Lines
                .OrderBy(l => l.LineNum)
                .Select(l => new SapInventoryTransferItemsRequests
                {
                    LineNum = l.LineNum,
                    ItemCode = l.ItemCode,
                    ItemDescription = l.ItemDescription,
                    AccountCode = l.AccountCode,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    DiscountPercent = l.DiscountPercent,
                    LineTotal = l.LineTotal,
                    TaxPercentagePerRow = l.TaxPercentagePerRow,
                    TaxTotal = l.TaxTotal,
                    TaxCode = l.TaxCode,
                    WTLiable = l.WTLiable,
                    TaxLiable = l.TaxLiable,
                    GrossTotal = l.GrossTotal,
                    WarehouseCode = l.WarehouseCode,
                    HSNEntry = l.HSNEntry,
                    SACEntry = l.SACEntry,
                    UoMCode = l.UoMCode,
                    UoMEntry = l.UoMEntry,
                    UnitsOfMeasurment = l.UnitsOfMeasurment,
                    InventoryQuantity = l.InventoryQuantity,
                    UseBaseUnits = l.UseBaseUnits,
                    ProjectCode = l.ProjectCode,
                    CostingCode = l.CostingCode,
                    CostingCode2 = l.CostingCode2,
                    CostingCode3 = l.CostingCode3,
                    CostingCode4 = l.CostingCode4,
                    CostingCode5 = l.CostingCode5,
                    UProdNo = l.UProdNo,
                    BaseType = l.BaseType,
                    BaseEntry = l.BaseEntry,
                    BaseLine = l.BaseLine,
                })
                .ToList();
        }

        return response;
    }

    private static void ApplyPaymentTermSlots(
        SapPurchaseOrdersResponse response,
        IEnumerable<PurchaseOrderPaymentTerm> terms)
    {
        foreach (var term in terms)
        {
            switch (term.Slot)
            {
                case 1:
                    response.UBasic1 = term.Basic; response.UGst1 = term.Gst;
                    response.UDes1 = term.Description; response.UStage1 = term.Stage; response.UType1 = term.Type;
                    break;
                case 2:
                    response.UBasic2 = term.Basic; response.UGst2 = term.Gst;
                    response.UDes2 = term.Description; response.UStage2 = term.Stage; response.UType2 = term.Type;
                    break;
                case 3:
                    response.UBasic3 = term.Basic; response.UGst3 = term.Gst;
                    response.UDes3 = term.Description; response.UStage3 = term.Stage; response.UType3 = term.Type;
                    break;
                case 4:
                    response.UBasic4 = term.Basic; response.UGst4 = term.Gst;
                    response.UDes4 = term.Description; response.UStage4 = term.Stage; response.UType4 = term.Type;
                    break;
                case 5:
                    response.UBasic5 = term.Basic; response.UGst5 = term.Gst;
                    response.UDes5 = term.Description; response.UStage5 = term.Stage; response.UType5 = term.Type;
                    break;
                case 6:
                    response.UBasic6 = term.Basic; response.UGst6 = term.Gst;
                    response.UDes6 = term.Description; response.UStage6 = term.Stage; response.UType6 = term.Type;
                    break;
                case 7:
                    response.UBasic7 = term.Basic; response.UGst7 = term.Gst;
                    response.UDes7 = term.Description; response.UStage7 = term.Stage; response.UType7 = term.Type;
                    break;
                case 8:
                    response.UBasic8 = term.Basic; response.UGst8 = term.Gst;
                    response.UDes8 = term.Description; response.UStage8 = term.Stage; response.UType8 = term.Type;
                    break;
                case 9:
                    response.UBasic9 = term.Basic; response.UGst9 = term.Gst;
                    response.UDes9 = term.Description; response.UStage9 = term.Stage; response.UType9 = term.Type;
                    break;
                case 10:
                    response.UBasic10 = term.Basic; response.UGst10 = term.Gst;
                    response.UDes10 = term.Description; response.UStage10 = term.Stage; response.UType10 = term.Type;
                    break;
                case 11:
                    response.UBasic11 = term.Basic; response.UGst11 = term.Gst;
                    response.UDes11 = term.Description; response.UStage11 = term.Stage; response.UType11 = term.Type;
                    break;
            }
        }
    }

    private static int? TryToInt(object? value)
    {
        if (value is null) return null;
        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is double d) return (int)d;
        if (int.TryParse(value.ToString(), out var parsed)) return parsed;
        return null;
    }
}
