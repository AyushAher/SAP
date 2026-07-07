using SapApi.Shared.Enums;

namespace SapApi.Shared
{
    public static class Constants
    {
        public static class CachedEndpoints
        {
            public static readonly HashSet<string> Endpoints =
            [
                SapApiUrls.GetAllBusinessPartners,
                SapApiUrls.GetAllItems,
                SapApiUrls.GetAllProductionOrders,
                SapApiUrls.GetAllSapPurchaseOrders + "?$filter=DocDate ge '2026-01-01'",
                SapApiUrls.GetAllWarehouses,
                SapApiUrls.GetAllSalesOrders(),
                SapApiUrls.GetAllSalesTaxCodes + "?$select=Name,Rate,Code",
                SapApiUrls.GetAllSalesTaxCodes,
                SapApiUrls.SapInventoryTransferRequests + "?$select=DocEntry,DocDate,DueDate,FromWarehouse,ToWarehouse,CardCode,CardName,ContactPerson",
                SapApiUrls.CreateInventoryGenExits + "?$select=DocEntry,CardCode,CardName,Project,DownPayment,DocTotal",
                SapApiUrls.GetAllProjectDetails,
                SapApiUrls.GetAllBpl,
            ];

            private static readonly System.Text.RegularExpressions.Regex SingleEntityReadPattern =
                new(@"/(PurchaseOrders|ProductionOrders|Orders|BusinessPartners)\(\d+\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

            public static bool ShouldCache(string url)
            {
                if (Endpoints.Any(e => url.Contains(e, StringComparison.OrdinalIgnoreCase)))
                    return true;

                if (!url.Contains("/b1s/v1/", StringComparison.Ordinal))
                    return false;

                if (url.Contains("/SQLQueries", StringComparison.OrdinalIgnoreCase)
                    || url.Contains("/Login", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (SingleEntityReadPattern.IsMatch(url))
                    return true;

                if (!url.Contains("$top=", StringComparison.OrdinalIgnoreCase))
                    return false;

                return !System.Text.RegularExpressions.Regex.IsMatch(url, @"\(\d+\)");
            }
        }

        public static class BankAccounts
        {
            public static Dictionary<string, string> Banks = new Dictionary<string, string>
            {
                { "_SYS00000000980", "PNB 4644008700000209_CC" },
                { "_SYS00000001410", "PNB 4644002100003527_CA" },
                { "_SYS00000001004", "*" },
                { "_SYS00000000977", "Petty Cash_Supa Factory" },
                { "_SYS00000001368", "Rounding Difference" },
                { "_SYS00000001507", "AxisBank 924020070943884_CA" },
                { "_SYS00000000979", "AxisBank 916020045857740_CA" },
                { "_SYS00000001409", "IndusIndBank 200008746034_CA" },
                { "_SYS00000001276", "Cash In Hand" },
                { "_SYS00000001407", "AxisBank 920020047289074_CA" },
                { "_SYS00000001508", "IndusIndBank 250923777777_CA" },
                { "_SYS00000001509", "AxisBank 926030001452578_CC" },
                { "_SYS00000001510", "AxisBank 926060049633190_TL" },
            };

            /// <summary>
            /// House bank G/L accounts allowed per business place (BPLID).
            /// </summary>
            public static readonly IReadOnlyDictionary<int, string[]> BanksByBplId = new Dictionary<int, string[]>
            {
                [1] = ["_SYS00000001410", "_SYS00000000980", "_SYS00000001004"], // Privilege Biksons
                [3] = ["_SYS00000001407", "_SYS00000001508", "_SYS00000001004"], // S M Projects
                [4] = ["_SYS00000001409", "_SYS00000001004"], // De Design Architects
                [5] = ["_SYS00000001507", "_SYS00000001509", "_SYS00000001510", "_SYS00000001004"], // Privilege Energex
            };

            public static IEnumerable<KeyValuePair<string, string>> GetBanksForBplId(int? bplId)
            {
                if (bplId is null || !BanksByBplId.TryGetValue(bplId.Value, out var accountKeys))
                    return [];

                return accountKeys
                    .Where(Banks.ContainsKey)
                    .Select(key => new KeyValuePair<string, string>(key, Banks[key]));
            }
        }

        public static class Roles
        {
            public const string SuperAdmin = "SuperAdmin";
            public const string Admin = "Admin";
            public const string Standard = "Standard";

            public static string CombinedString(params string[] roles)
            {
                return string.Join(",", roles);
            }
        }

        public static IReadOnlyDictionary<ApprovalDocumentType, List<string>> ApprovalDocFields = new Dictionary<ApprovalDocumentType, List<string>>
        {
            [ApprovalDocumentType.None] = [],
            [ApprovalDocumentType.PurchaseOrder] = ["DocTotal"],
            [ApprovalDocumentType.ProductionOrder] = ["ItemNo", "PlannedQuantity", "CompletedQuantity", "Warehouse"],
            [ApprovalDocumentType.StagewisePayments_DP] = ["DocTotal"],
            [ApprovalDocumentType.Payments] = ["DocTotal"],
            [ApprovalDocumentType.InventoryItemsTransfer] = ["Warehouse"],
            [ApprovalDocumentType.IssueForProduction] = ["Warehouse"],
        };

        public static IReadOnlyList<string> ApprovalOperator = ["GreaterThan", "GreaterThanOrEqual", "LessThan", "LessThanOrEqual", "Equal"];
        public static string SapServiceLayerUrl { get; set; } = string.Empty;
        public static string AuthServiceUrl { get; set; } = string.Empty;
        public const string SapBaseUrl = "/b1s/v1";

        // TODO: Get from app settings
        public const string DateTimeFormat = "dd/MM/yyyy hh:mm";

        public static class SapBoolean
        {
            public const string SapTrue = "tYES";
            public const string SapFalse = "tNO";

            public static async Task<IEnumerable<string?>> SearchFunc(string arg, CancellationToken cancellationToken)
            {
                string[] booleanValues = [SapTrue, SapFalse];
                return booleanValues.Where(x => x.Contains(arg)).ToList();
            }
        }

        public class PurchaseOrderDocType
        {
            public const string Document_Service = "dDocument_Service";
            public const string Document_Item = "dDocument_Items";
        }

        public static class SapBusinessPartnerType
        {
            public const string Customer = "cCustomer";
            public const string Vendor = "cSupplier";
        }
        public static class SapProductionOrderStatus
        {
            public const string Planned = "boposPlanned";
            public const string Released = "boposReleased";
            public const string Closed = "boposClosed";
            public const string Cancelled = "boposCancelled";

            public static string GetDisplay(string? status)
            {
                return status switch
                {
                    Planned => "Planned",
                    Released => "Released",
                    Closed => "Closed",
                    Cancelled => "Cancelled",
                    _ => status,
                };
            }
        }
        public static class SapPaymentMeansType
        {
            public const string BankTransfer = "pmtBankTransfer";
            public const string Check = "pmtChecks";
            public const string CreditCard = "pmtCreditCard";
            public const string Cash = "pmtCash";

            public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [BankTransfer] = "Bank Transfer",
                [Check] = "Check",
                [CreditCard] = "Credit Card",
                [Cash] = "Cash",
            };
        }
        public static class SapApiUrls
        {
            public static string Login = SapServiceLayerUrl + SapBaseUrl + "/Login";
            public static string Logout = SapServiceLayerUrl + SapBaseUrl + "/Logout";
            public static string SapInventoryTransferRequests =
                SapServiceLayerUrl + SapBaseUrl + "/InventoryTransferRequests";
            public static string GetAllWarehouses = SapServiceLayerUrl + SapBaseUrl +
                                                    "/Warehouses?$select=WarehouseCode,State,City,Location";
            public static string GetAllBusinessPartners = SapServiceLayerUrl + SapBaseUrl + "/BusinessPartners";
            public static string GetPurchaseDownPaymentByDocNum(string docEntry) => SapServiceLayerUrl + SapBaseUrl + $"/PurchaseDownPayments?$filter=DocNum eq {docEntry}";

            public static string GetAllCustomers = SapServiceLayerUrl + SapBaseUrl + "/BusinessPartners?$filter=CardType eq 'cCustomer'";
            public static string GetAllProductionOrders = SapServiceLayerUrl + SapBaseUrl + "/ProductionOrders";
            public static string GetProductionOrders(string id) => SapServiceLayerUrl + SapBaseUrl + $"/ProductionOrders({id})";
            public static string UpdateProductionOrders(int? id) =>
                SapServiceLayerUrl + SapBaseUrl + $"/ProductionOrders({id})";
            public static string CreateProductionOrder = SapServiceLayerUrl + SapBaseUrl + $"/ProductionOrders";
            public static string CreateInventoryGenExits = SapServiceLayerUrl + SapBaseUrl + "/InventoryGenExits";
            public static string SaveBusinessPartners = SapServiceLayerUrl + SapBaseUrl + "/BusinessPartners";
            public static string GetAllWithholdingTaxDataCollection = SapServiceLayerUrl + SapBaseUrl + "/WithholdingTaxCodes";
            public static string GetAllItems = SapServiceLayerUrl + SapBaseUrl +
                                               "/Items?$select=ItemCode,ItemName,ItemsGroupCode,InventoryItem,InventoryUOM,InventoryWeight";
            public static string ItemsCollection = SapServiceLayerUrl + SapBaseUrl + "/Items";
            public static string WarehousesCollection = SapServiceLayerUrl + SapBaseUrl + "/Warehouses";
            public static string SalesTaxCodesCollection = SapServiceLayerUrl + SapBaseUrl + "/SalesTaxCodes";
            public static string ProjectsCollection = SapServiceLayerUrl + SapBaseUrl + "/Projects";
            public static string BusinessPlacesCollection = SapServiceLayerUrl + SapBaseUrl + "/BusinessPlaces";
            public static string BusinessPartnersCollection = SapServiceLayerUrl + SapBaseUrl + "/BusinessPartners";
            public static string OrdersCollection = SapServiceLayerUrl + SapBaseUrl + "/Orders";
            public static string WithholdingTaxCodesCollection = SapServiceLayerUrl + SapBaseUrl + "/WithholdingTaxCodes";
            public static string GetAllPurchaseDownPayment = SapServiceLayerUrl + SapBaseUrl + "/PurchaseDownPayments";
            public static string PurchaseDownPayment = SapServiceLayerUrl + SapBaseUrl + "/PurchaseDownPayments";
            public static string UpdatePurchaseDownPayment(string id) =>
                $"{SapServiceLayerUrl}{SapBaseUrl}/PurchaseDownPayments({id})";
            public static string CancelPurchaseDownPayment(string docEntry) =>
                $"{SapServiceLayerUrl}{SapBaseUrl}/PurchaseDownPayments({docEntry})/Cancel";
            public static string GetAllSapPurchaseOrders = SapServiceLayerUrl + SapBaseUrl + "/PurchaseOrders";
            public static string UpdateSapPurchaseOrders(int? docEntry) => SapServiceLayerUrl + SapBaseUrl + "/PurchaseOrders" + $"({docEntry})";
            public static string PurchaseDeliveryNotes = SapServiceLayerUrl + SapBaseUrl + "/PurchaseDeliveryNotes";
            public static string GetAllSalesTaxCodes = SapServiceLayerUrl + SapBaseUrl + "/SalesTaxCodes";
            public static string GetAllPurchaseInvoices = SapServiceLayerUrl + SapBaseUrl + "/PurchaseInvoices";
            public static string GetAllPurchaseDeliveryNotes = SapServiceLayerUrl + SapBaseUrl + "/PurchaseDeliveryNotes";
            public static string GetAllSalesOrders(string? customerId = null) => SapServiceLayerUrl + SapBaseUrl + $"/Orders?$select=DocNum,Project,CardName,CardCode,DocEntry,DocumentLines,NumAtCard{(customerId is not null ? "&$filter=CardCode eq '{customerId}'" : "")}";
            public static string GetSalesOrders(string id) => SapServiceLayerUrl + SapBaseUrl + $"/Orders({id})?$select=DocNum,Project,CardName";
            public static string SapInventoryTransferRequestsCancel(string docEntry) =>
                SapServiceLayerUrl + SapBaseUrl + $"/InventoryTransferRequests({docEntry})/Close";
            public static string SapInventoryTransferRequestsClose(string docEntry) => SapServiceLayerUrl + SapBaseUrl +
                $"/InventoryTransferRequests({docEntry})/Cancel";

            public static string CancelVendorPayment(string docEntry) => SapServiceLayerUrl + SapBaseUrl + $"/VendorPayments({docEntry})/Cancel";
            public static string GetVendorPayment(string docEntry) => SapServiceLayerUrl + SapBaseUrl + $"/VendorPayments({docEntry})";
            public static string GetVendorPaymentByDocEntry(string docEntry) => SapServiceLayerUrl + SapBaseUrl + $"/VendorPayments?$filter=DocNum eq {docEntry}";

            public static string CreateVendorPayments = SapServiceLayerUrl + SapBaseUrl + "/VendorPayments";
            public static string GetAllProjectDetails = SapServiceLayerUrl + SapBaseUrl + "/Projects";
            public static string GetAllBpl = SapServiceLayerUrl + SapBaseUrl + "/BusinessPlaces";

        }
        public static class SapVendorPaymentInvoiceType
        {
            public const string Invoice = "it_PurchaseInvoice";
            public const string DownPayment = "it_PurchaseDownPayment";
        }


        public static class SapSqlQueryName
        {
            public static string GetProductionOrderLines = "GetProductionOrderLines";
            public static string DeleteProductionOrderLines = "DeleteProductionOrderLines";
        }
    }
}
