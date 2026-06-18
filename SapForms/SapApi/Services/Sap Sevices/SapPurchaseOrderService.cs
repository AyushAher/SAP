using SapApi.Services.Helpers;
using SapApi.Modals;
using SapApi.Modals.Responses;
using SapApi.Modals.Responses.Sap;

namespace SapApi.Services
{
    public class SapPurchaseOrderService(IHttpRequestHandler requestHandler)
    {
        public Task<GetAllSapPurchaseOrdersResponse?> GetAllPurchaseOrders()
        {
            return requestHandler.GetAsync<GetAllSapPurchaseOrdersResponse>(Constants.SapApiUrls.GetAllSapPurchaseOrders);
        }

        public Task<SapPurchaseOrdersResponse?> GetPurchaseOrders(string id)
        {
            return requestHandler.GetAsync<SapPurchaseOrdersResponse>(
                Constants.SapApiUrls.GetAllSapPurchaseOrders + $"({id})");
        }
    }
}