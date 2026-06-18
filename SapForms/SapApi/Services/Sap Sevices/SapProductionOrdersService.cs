using SapApi.Services.Helpers;
using SapApi.Modals;
using SapApi.Modals.Responses.Sap;

namespace SapApi.Services
{
    public class SapProductionOrdersService(IHttpRequestHandler httpRequestHandler)
    {
        public Task<GetAllSapProductionOrdersResponse?> GetAllProductionOrders()
        {
            return httpRequestHandler.GetAsync<GetAllSapProductionOrdersResponse>(Constants.SapApiUrls
                .GetAllProductionOrders);
        }

        public Task<SapProductionOrdersResponse?> UpdateProductionOrderAsync(SapProductionOrdersResponse addedLines)
        {
            return httpRequestHandler.PutAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                Constants.SapApiUrls.GetProductionOrders(addedLines.AbsoluteEntry.ToString()), addedLines);
        }
    }
}