using SapApi.Shared.Sap;

namespace SapApi.Infrastructure.Sap;

public static class SapPaginationProfiles
{
    public static SapPaginationOptions PurchaseOrders => new()
    {
        BaseFilter = "DocDate ge '2026-01-01'",
        Select = "DocEntry,DocNum,CardCode,CardName,Project,DocTotal,DocumentStatus,DocDate,VatSum",
        DefaultSortField = "DocEntry",
        DefaultSortDirection = "desc",
        FieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DocEntry"] = "DocEntry",
            ["DocNum"] = "DocNum",
            ["CardCode"] = "CardCode",
            ["CardName"] = "CardName",
            ["Project"] = "Project",
            ["DocTotal"] = "DocTotal",
            ["DocumentStatus"] = "DocumentStatus",
        },
    };

    public static SapPaginationOptions ProductionOrders => new()
    {
        Select = "AbsoluteEntry,DocumentNumber,ItemNo,ProductDescription,PlannedQuantity,Project,Warehouse,ProductionOrderStatus",
        DefaultSortField = "AbsoluteEntry",
        DefaultSortDirection = "desc",
        FieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AbsoluteEntry"] = "AbsoluteEntry",
            ["DocumentNumber"] = "DocumentNumber",
            ["ItemNo"] = "ItemNo",
            ["ProductDescription"] = "ProductDescription",
            ["PlannedQuantity"] = "PlannedQuantity",
            ["Project"] = "Project",
            ["Warehouse"] = "Warehouse",
            ["ProductionOrderStatus"] = "ProductionOrderStatus",
        },
    };

    public static SapPaginationOptions InventoryTransfers => new()
    {
        Select = "DocEntry,DocDate,DueDate,FromWarehouse,ToWarehouse,CardCode,CardName,ContactPerson",
        DefaultSortField = "DocEntry",
        DefaultSortDirection = "desc",
        FieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DocEntry"] = "DocEntry",
            ["DocDate"] = "DocDate",
            ["FromWarehouse"] = "FromWarehouse",
            ["ToWarehouse"] = "ToWarehouse",
            ["CardCode"] = "CardCode",
            ["CardName"] = "CardName",
        },
    };

    public static SapPaginationOptions Items => new()
    {
        Select = "ItemCode,ItemName,ItemsGroupCode,InventoryItem,InventoryUOM",
        DefaultSortField = "ItemCode",
        DefaultSortDirection = "asc",
        SearchOrFields = ["ItemCode", "ItemName"],
        SearchCodeFields = ["ItemCode"],
        SearchTextFields = ["ItemName"],
        FieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ItemCode"] = "ItemCode",
            ["ItemName"] = "ItemName",
        },
    };

    public static SapPaginationOptions Warehouses => new()
    {
        Select = "WarehouseCode,State,City,Location",
        DefaultSortField = "WarehouseCode",
        DefaultSortDirection = "asc",
        SearchOrFields = ["WarehouseCode", "City"],
        SearchCodeFields = ["WarehouseCode"],
        SearchTextFields = ["City"],
        FieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WarehouseCode"] = "WarehouseCode",
            ["City"] = "City",
        },
    };

    public static SapPaginationOptions TaxCodes => new()
    {
        Select = "Code,Name,Rate",
        DefaultSortField = "Code",
        DefaultSortDirection = "asc",
        SearchOrFields = ["Code", "Name"],
        SearchCodeFields = ["Code"],
        SearchTextFields = ["Name"],
        FieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Code"] = "Code",
            ["Name"] = "Name",
        },
    };

    public static SapPaginationOptions Projects => new()
    {
        Select = "Code,Name",
        DefaultSortField = "Code",
        DefaultSortDirection = "asc",
        SearchOrFields = ["Code", "Name"],
        SearchCodeFields = ["Code"],
        SearchTextFields = ["Name"],
        FieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Code"] = "Code",
            ["Name"] = "Name",
        },
    };

    public static SapPaginationOptions BusinessPlaces => new()
    {
        Select = "BPLID,BPLName,Address",
        DefaultSortField = "BPLID",
        DefaultSortDirection = "asc",
        SearchOrFields = ["BPLName"],
        SearchTextFields = ["BPLName"],
        FieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BPLID"] = "BPLID",
            ["BPLName"] = "BPLName",
        },
    };

    public static SapPaginationOptions Vendors => new()
    {
        BaseFilter = "CardType eq 'cSupplier'",
        Select = "CardCode,CardName,CardType",
        DefaultSortField = "CardCode",
        DefaultSortDirection = "asc",
        SearchOrFields = ["CardCode", "CardName"],
        SearchCodeFields = ["CardCode"],
        SearchTextFields = ["CardName"],
        FieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CardCode"] = "CardCode",
            ["CardName"] = "CardName",
        },
    };

    public static SapPaginationOptions Customers => new()
    {
        BaseFilter = "CardType eq 'cCustomer'",
        Select = "CardCode,CardName,CardType",
        DefaultSortField = "CardCode",
        DefaultSortDirection = "asc",
        SearchOrFields = ["CardCode", "CardName"],
        SearchCodeFields = ["CardCode"],
        SearchTextFields = ["CardName"],
        FieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CardCode"] = "CardCode",
            ["CardName"] = "CardName",
        },
    };

    public static SapPaginationOptions InventoryGenExits => new()
    {
        Select = "DocEntry,DocNum,CardCode,CardName,Project,DownPayment,DocTotal,DocDate",
        DefaultSortField = "DocEntry",
        DefaultSortDirection = "desc",
        SearchOrFields = ["CardCode", "CardName", "Project"],
        SearchCodeFields = ["CardCode", "Project"],
        SearchTextFields = ["CardName"],
        FieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DocEntry"] = "DocEntry",
            ["DocNum"] = "DocNum",
            ["CardCode"] = "CardCode",
            ["CardName"] = "CardName",
            ["Project"] = "Project",
        },
    };

    public static SapPaginationOptions SalesOrders(string? customerId = null) => new()
    {
        BaseFilter = customerId is null ? null : $"CardCode eq '{customerId.Replace("'", "''")}'",
        Select = "DocNum,Project,CardName,CardCode,DocEntry,NumAtCard",
        DefaultSortField = "DocNum",
        DefaultSortDirection = "desc",
        SearchOrFields = ["DocNum", "NumAtCard", "CardName"],
        SearchCodeFields = ["DocNum", "NumAtCard"],
        SearchTextFields = ["CardName"],
        FieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DocNum"] = "DocNum",
            ["DocEntry"] = "DocEntry",
            ["CardCode"] = "CardCode",
            ["CardName"] = "CardName",
            ["NumAtCard"] = "NumAtCard",
        },
    };
}
