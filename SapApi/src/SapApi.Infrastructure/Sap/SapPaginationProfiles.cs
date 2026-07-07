using SapApi.Shared.Sap;

namespace SapApi.Infrastructure.Sap;

public static class SapPaginationProfiles
{
    /// <summary>
    /// Header + payment-term UDF fields only (no DocumentLines). Used for stage-wise payment page load.
    /// </summary>
    public const string PurchaseOrderPaymentPageSelect =
        "DocEntry,DocNum,DocType,CardCode,CardName,Project,DocTotal,VatSum,DocumentStatus,DocDate,BPL_IDAssignedToInvoice," +
        "U_B1,U_B2,U_B3,U_B4,U_B5,U_B6,U_B7,U_B8,U_B9,U_B10,U_B11," +
        "U_G1,U_G2,U_G3,U_G4,U_G5,U_G6,U_G7,U_G8,U_G9,U_G10,U_G11," +
        "U_D1,U_D2,U_D3,U_D4,U_D5,U_D6,U_D7,U_D8,U_D9,U_D10,U_D11," +
        "U_S1,U_S2,U_S3,U_S4,U_S5,U_S6,U_S7,U_S8,U_S9,U_S10,U_S11," +
        "U_T1,U_T2,U_T3,U_T4,U_T5,U_T6,U_T7,U_T8,U_T9,U_T10,U_T11";

    /// <summary>
    /// Payment page fields plus document lines required for down-payment creation.
    /// </summary>
    public const string PurchaseOrderPaymentOperationsSelect =
        PurchaseOrderPaymentPageSelect + ",DocumentLines";

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
