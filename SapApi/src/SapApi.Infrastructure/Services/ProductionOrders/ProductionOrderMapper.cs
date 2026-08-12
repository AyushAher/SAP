using System.Globalization;
using System.Text.Json;
using SapApi.Domain.Entities;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services.ProductionOrders;

public static class ProductionOrderMapper
{
    /// <summary>
    /// Mid-string list filters: one UI column searches both the code and the name, so filtering
    /// the mirrored list never needs a live SAP master-data lookup.
    /// </summary>
    public static readonly Dictionary<string, string[]> ListOrFieldAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["itemNo"] = ["ItemNo", "ProductDescription"],
        ["itemNumber"] = ["ItemNo", "ProductDescription"],
        ["productDescription"] = ["ItemNo", "ProductDescription"],
        ["customerCode"] = ["CustomerCode", "CustomerName"],
        ["customerName"] = ["CustomerCode", "CustomerName"],
        ["cardCode"] = ["CustomerCode", "CustomerName"],
        ["cardName"] = ["CustomerCode", "CustomerName"],
        ["project"] = ["Project", "ProjectName"],
        ["projectName"] = ["Project", "ProjectName"],
        ["drawingNo"] = ["DrawingNo"],
        ["warehouse"] = ["Warehouse"],
        ["productionCategory"] = ["ProductionCategory"],
    };

    public static void ApplyHeader(ProductionOrder entity, SapProductionOrdersResponse sap, DateTime syncedAtUtc)
    {
        entity.IsDeleted = false;
        entity.AbsoluteEntry = sap.AbsoluteEntry ?? entity.AbsoluteEntry;
        entity.DocumentNumber = sap.DocumentNumber;
        entity.Series = sap.Series;
        entity.ItemNo = sap.ItemNumber;
        entity.ProductDescription = sap.ProductDescription;
        entity.Status = sap.Status;
        entity.Type = sap.Type;
        entity.ProductionCategory = NullIfBlank(sap.ProductionCategory);
        entity.DrawingNo = NullIfBlank(sap.DrawingNo);
        entity.PlannedQuantity = sap.PlannedQuantity;
        entity.CompletedQuantity = sap.CompletedQuantity;
        entity.RejectedQuantity = sap.RejectedQuantity;
        entity.Warehouse = sap.Warehouse;
        entity.InventoryUom = sap.InventoryUom;
        entity.UoMEntry = sap.UoMEntry;
        entity.CustomerCode = NullIfBlank(sap.CustomerCode);
        entity.Project = NullIfBlank(sap.Project);
        // U_PrjName is a real OWOR user field; keep it when SAP has one and let the caller fill
        // the master-data name only where it is blank.
        entity.ProjectName = NullIfBlank(sap.ProjectName) ?? entity.ProjectName;
        entity.SalesOrderDocEntry = sap.SalesOrderDocEntry;
        entity.SalesOrderDocNum = sap.SalesOrderDocNum;
        entity.ProductionOrderOrigin = sap.ProductionOrderOrigin;
        entity.PostingDate = sap.PostingDate == default ? null : sap.PostingDate;
        entity.DueDate = sap.DueDate;
        entity.StartDate = sap.StartDate;
        entity.ReleaseDate = sap.ReleaseDate;
        entity.ClosingDate = sap.ClosingDate;
        entity.CreationDate = sap.CreationDate;
        entity.Remarks = sap.Remarks;
        entity.JournalRemarks = sap.JournalRemarks;
        entity.PickRemarks = sap.PickRemarks;
        entity.Printed = sap.Printed;
        entity.Priority = sap.Priority;
        entity.UserSignature = sap.UserSignature;
        entity.TransactionNumber = sap.TransactionNumber;
        entity.AttachmentEntry = sap.AttachmentEntry;
        entity.RoutingDateCalculation = sap.RoutingDateCalculation;
        entity.UpdateAllocation = sap.UpdateAllocation;
        entity.DistributionRule = NullIfBlank(sap.DistributionRule);
        entity.DistributionRule2 = NullIfBlank(sap.DistributionRule2);
        entity.DistributionRule3 = NullIfBlank(sap.DistributionRule3);
        entity.DistributionRule4 = NullIfBlank(sap.DistributionRule4);
        entity.DistributionRule5 = NullIfBlank(sap.DistributionRule5);
        entity.SyncedAtUtc = syncedAtUtc;
        entity.LastModifiedOn = syncedAtUtc;
    }

    public static List<ProductionOrderLine> MapLines(int productionOrderId, IEnumerable<SapProductionOrderLines>? lines)
    {
        if (lines is null)
            return [];

        return lines.Select((line, index) => new ProductionOrderLine
        {
            ProductionOrderId = productionOrderId,
            LineNumber = line.LineNumber ?? index,
            ItemNo = line.ItemNo,
            ItemName = line.ItemName,
            ItemType = ToText(line.ItemType),
            LineText = line.LineText,
            BaseQuantity = line.BaseQuantity,
            PlannedQuantity = line.PlannedQuantity,
            IssuedQuantity = line.IssuedQuantity,
            AdditionalQuantity = line.AdditionalQuantity,
            ProductionOrderIssueType = line.ProductionOrderIssueType,
            Warehouse = line.Warehouse,
            VisualOrder = line.VisualOrder,
            LocationCode = line.LocationCode,
            Project = line.Project,
            UoMEntry = line.UoMEntry,
            UoMCode = ToUoMCode(line.UoMCode),
            WipAccount = line.WipAccount,
            StageId = line.StageId,
            RequiredDays = line.RequiredDays,
            ResourceAllocation = line.ResourceAllocation,
            StartDate = ToDateTime(line.StartDate),
            EndDate = ToDateTime(line.EndDate),
            DistributionRule = line.DistributionRule,
            DistributionRule2 = line.DistributionRule2,
            DistributionRule3 = line.DistributionRule3,
            DistributionRule4 = line.DistributionRule4,
            DistributionRule5 = line.DistributionRule5,
            FreeText = line.FreeText,
            DocNum = line.DocNum,
        }).ToList();
    }

    public static SapProductionOrdersResponse ToSapResponse(ProductionOrder entity, bool includeLines)
    {
        var response = new SapProductionOrdersResponse
        {
            AbsoluteEntry = entity.AbsoluteEntry,
            DocumentNumber = entity.DocumentNumber,
            Series = entity.Series,
            ItemNumber = entity.ItemNo,
            ProductDescription = entity.ProductDescription,
            Status = entity.Status,
            Type = entity.Type,
            ProductionCategory = entity.ProductionCategory ?? string.Empty,
            DrawingNo = entity.DrawingNo ?? string.Empty,
            PlannedQuantity = entity.PlannedQuantity ?? 0,
            CompletedQuantity = entity.CompletedQuantity ?? 0,
            RejectedQuantity = entity.RejectedQuantity ?? 0,
            Warehouse = entity.Warehouse,
            InventoryUom = entity.InventoryUom,
            UoMEntry = entity.UoMEntry,
            CustomerCode = entity.CustomerCode,
            CustomerName = entity.CustomerName,
            Project = entity.Project,
            ProjectName = entity.ProjectName,
            SalesOrderDocEntry = entity.SalesOrderDocEntry,
            SalesOrderDocNum = entity.SalesOrderDocNum,
            ProductionOrderOrigin = entity.ProductionOrderOrigin,
            PostingDate = entity.PostingDate ?? default,
            DueDate = entity.DueDate,
            StartDate = entity.StartDate,
            ReleaseDate = entity.ReleaseDate,
            ClosingDate = entity.ClosingDate,
            CreationDate = entity.CreationDate,
            Remarks = entity.Remarks,
            JournalRemarks = entity.JournalRemarks,
            PickRemarks = entity.PickRemarks,
            Printed = entity.Printed,
            Priority = entity.Priority ?? 100,
            UserSignature = entity.UserSignature,
            TransactionNumber = entity.TransactionNumber,
            AttachmentEntry = entity.AttachmentEntry,
            RoutingDateCalculation = entity.RoutingDateCalculation,
            UpdateAllocation = entity.UpdateAllocation,
            DistributionRule = entity.DistributionRule,
            DistributionRule2 = entity.DistributionRule2,
            DistributionRule3 = entity.DistributionRule3,
            DistributionRule4 = entity.DistributionRule4,
            DistributionRule5 = entity.DistributionRule5,
            ProductionOrderLines = includeLines ? ToSapLines(entity.AbsoluteEntry, entity.Lines) : null,
        };

        return response;
    }

    public static List<SapProductionOrderLines> ToSapLines(
        int absoluteEntry,
        IEnumerable<ProductionOrderLine>? lines) =>
        (lines ?? [])
            .OrderBy(l => l.VisualOrder ?? l.LineNumber)
            .ThenBy(l => l.LineNumber)
            .Select(l => new SapProductionOrderLines
            {
                DocumentAbsoluteEntry = absoluteEntry,
                LineNumber = l.LineNumber,
                ItemNo = l.ItemNo,
                ItemName = l.ItemName,
                ItemType = l.ItemType,
                LineText = l.LineText,
                BaseQuantity = l.BaseQuantity,
                PlannedQuantity = l.PlannedQuantity ?? 0,
                IssuedQuantity = l.IssuedQuantity ?? 0,
                AdditionalQuantity = l.AdditionalQuantity,
                ProductionOrderIssueType = l.ProductionOrderIssueType,
                Warehouse = l.Warehouse,
                VisualOrder = l.VisualOrder,
                LocationCode = l.LocationCode,
                Project = l.Project,
                UoMEntry = l.UoMEntry,
                UoMCode = l.UoMCode,
                WipAccount = l.WipAccount,
                StageId = l.StageId,
                RequiredDays = l.RequiredDays,
                ResourceAllocation = l.ResourceAllocation,
                StartDate = l.StartDate,
                EndDate = l.EndDate,
                DistributionRule = l.DistributionRule,
                DistributionRule2 = l.DistributionRule2,
                DistributionRule3 = l.DistributionRule3,
                DistributionRule4 = l.DistributionRule4,
                DistributionRule5 = l.DistributionRule5,
                FreeText = l.FreeText,
                DocNum = l.DocNum,
            })
            .ToList();

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ToText(object? value) => value switch
    {
        null => null,
        string s => NullIfBlank(s),
        JsonElement { ValueKind: JsonValueKind.String } je => NullIfBlank(je.GetString()),
        JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
        JsonElement je => je.ToString(),
        _ => NullIfBlank(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };

    /// <summary>
    /// SAP sends ProductionOrderLine.UoMCode as a whole number. Anything else (an inventory UoM
    /// name such as "KG") is dropped rather than mirrored, because SAP rejects it on write.
    /// </summary>
    public static int? ToUoMCode(object? value) =>
        SapApi.Shared.Sap.SapProductionOrderUoMNormalizer.NormalizeUoMCode(value) as int?;

    public static DateTime? ToDateTime(object? value) => value switch
    {
        null => null,
        DateTime dt => dt,
        DateTimeOffset dto => dto.UtcDateTime,
        JsonElement { ValueKind: JsonValueKind.String } je => ParseDate(je.GetString()),
        JsonElement => null,
        string s => ParseDate(s),
        _ => null,
    };

    private static DateTime? ParseDate(string? text) =>
        DateTime.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
}
