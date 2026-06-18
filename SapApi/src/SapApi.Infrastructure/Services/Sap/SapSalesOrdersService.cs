using SapApi.Domain.Interfaces;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared;
namespace SapApi.Infrastructure.Services.Sap
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
