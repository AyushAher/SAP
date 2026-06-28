using SapApi.Modals.Requests.Account;

namespace SapApi.Modals
{
    public static class Constants
    {
        public static string SapServiceLayerUrl { get; set; }
        public static string AuthServiceUrl { get; set; }
        public static string SapBaseUrl = "/b1s/v1";

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

        public static class SapBusinessPartnerType
        {
            public const string Customer = "cCustomer";
            public const string Vendor = "cVendor";
        }

        public static class SapPaymentMeansType
        {
            public const string BankTransfer = "pmtBankTransfer";
        }
        public static class SapApiUrls
        {
            public static string Login = SapServiceLayerUrl + SapBaseUrl + "/Login";

            public static string SapInventoryTransferRequests =
                SapServiceLayerUrl + SapBaseUrl + "/InventoryTransferRequests";

            public static string GetAllWarehouses = SapServiceLayerUrl + SapBaseUrl +
                                                    "/Warehouses?$select=WarehouseCode,State,City,Location";

            public static string GetAllBusinessPartners = SapServiceLayerUrl + SapBaseUrl + "/BusinessPartners";
            public static string GetAllProductionOrders = SapServiceLayerUrl + SapBaseUrl + "/ProductionOrders";

            public static string UpdateProductionOrders(int id) =>
                SapServiceLayerUrl + SapBaseUrl + $"/ProductionOrders({id})";
            public static string CreateInventoryGenExits = SapServiceLayerUrl + SapBaseUrl + "/InventoryGenExits";
            public static string SaveBusinessPartners = SapServiceLayerUrl + SapBaseUrl + "/BusinessPartners";
            public static string GetAllItems = SapServiceLayerUrl + SapBaseUrl +
                                               "/Items?$select=ItemCode,ItemName,ItemsGroupCode,InventoryItem";

            public static string GetAllPurchaseDownPayment = SapServiceLayerUrl + SapBaseUrl + "/PurchaseDownPayments";
            public static string PurchaseDownPayment = SapServiceLayerUrl + SapBaseUrl + "/PurchaseDownPayments";

            public static string UpdatePurchaseDownPayment(string id) =>
                $"{SapServiceLayerUrl}{SapBaseUrl}/PurchaseDownPayments({id})";

            public static string GetAllSapPurchaseOrders = SapServiceLayerUrl + SapBaseUrl + "/PurchaseOrders";
            public static string GetAllSalesTaxCodes = SapServiceLayerUrl + SapBaseUrl + "/SalesTaxCodes";

            public static string SapInventoryTransferRequestsCancel(string docEntry) =>
                SapServiceLayerUrl + SapBaseUrl + $"/InventoryTransferRequests({docEntry})/Close";

            public static string SapInventoryTransferRequestsClose(string docEntry) => SapServiceLayerUrl + SapBaseUrl +
                $"/InventoryTransferRequests({docEntry})/Cancel";

            public static string CreateVendorPayments = SapServiceLayerUrl + SapBaseUrl + "/VendorPayments";
        }

        public static class AuthServiceApiUrls
        {
            private static readonly string BaseUrl = AuthServiceUrl;

            public static string LoginUrl(LoginRequest loginRequest) => BaseUrl +
                                                                        $"/api/user/login?username={loginRequest.UserName}&password={loginRequest.Password}";

            public static string RegistrationUrl(string password) =>
                BaseUrl + $"/api/user/register?password={password}";
        }

    }
    public enum PurchaseRequestPaymentTermsType
    {
        Advance,
        AgainstProforma,
        AgainstInvoice,
        Retention
    }
}