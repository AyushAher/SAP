using SapApi.Domain.Entities;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services.Items;

public static class ItemMapper
{
    public static void ApplyFromSap(Item entity, ItemsResponse sap, DateTime syncedAtUtc)
    {
        entity.ItemName = sap.ItemName;
        entity.ItemGroupCode = sap.ItemGroupCode;
        entity.ItemsGroupCode = sap.ItemsGroupCode;
        entity.InventoryItem = sap.InventoryItem;
        entity.InventoryUom = sap.InventoryUom;
        entity.PurchaseUnit = sap.PurchaseUnit;
        entity.PurchaseItemsPerUnit = sap.PurchaseItemsPerUnit;
        entity.InventoryWeight = sap.InventoryWeight;
        entity.UoMGroupEntry = sap.UoMGroupEntry;
        entity.InventoryUoMEntry = sap.InventoryUoMEntry;
        entity.DefaultPurchasingUoMEntry = sap.DefaultPurchasingUoMEntry;
        entity.ChapterID = sap.ChapterID;
        entity.DefaultWarehouse = sap.DefaultWarehouse;
        entity.GstRelevant = sap.GstRelevant;
        entity.PurchaseVatGroup = sap.PurchaseVatGroup;
        entity.SyncedAtUtc = syncedAtUtc;
        entity.LastModifiedOn = syncedAtUtc;
    }

    public static ItemsResponse ToSapResponse(Item entity) => new()
    {
        ItemCode = entity.ItemCode,
        ItemName = entity.ItemName,
        ItemGroupCode = entity.ItemGroupCode,
        ItemsGroupCode = entity.ItemsGroupCode,
        InventoryItem = entity.InventoryItem,
        InventoryUom = entity.InventoryUom,
        PurchaseUnit = entity.PurchaseUnit,
        PurchaseItemsPerUnit = entity.PurchaseItemsPerUnit,
        InventoryWeight = entity.InventoryWeight,
        UoMGroupEntry = entity.UoMGroupEntry,
        InventoryUoMEntry = entity.InventoryUoMEntry,
        DefaultPurchasingUoMEntry = entity.DefaultPurchasingUoMEntry,
        ChapterID = entity.ChapterID,
        DefaultWarehouse = entity.DefaultWarehouse,
        GstRelevant = entity.GstRelevant,
        PurchaseVatGroup = entity.PurchaseVatGroup,
    };
}
