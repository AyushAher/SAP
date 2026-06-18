using SapForm.Services.Helpers;
using Shared.Responses.Sap;
using Shared;
namespace SapForm.Services.Sap_Sevices
{
    public class SapSalesOrdersService(IHttpRequestHandler requestHandler)
    {
        public Task<GetAllSapSalesOrderResponse?> GetAllSalesOrders(string? customerId = null)
        {
            return requestHandler.GetAsync<GetAllSapSalesOrderResponse>(Constants.SapApiUrls.GetAllSalesOrders(customerId));
        }
        public Task<GetAllSapSalesOrderResponse?> GetSalesOrders(string docId)
        {
            return requestHandler.GetAsync<GetAllSapSalesOrderResponse>(Constants.SapApiUrls.GetSalesOrders(docId));
        }
    }
}
