using SapApi.Services.Helpers;
using SapApi.Modals;
using SapApi.Modals.Requests;
using SapApi.Modals.Responses.Sap;

namespace SapApi.Services
{
    public class SapInventoryGenExitsService(IHttpRequestHandler httpRequestHandler)
    {
        public Task<SapBaseResponse?> CreateAsync(SapInventoryGenExitRequestOrderRequest request)
        {
            return httpRequestHandler.PostAsync<SapInventoryGenExitRequestOrderRequest, SapBaseResponse>(Constants.SapApiUrls.CreateInventoryGenExits, request);
        }

        public Task<GetAllSapIssueForProductionResponse?> GetAll()
        {
            return httpRequestHandler.GetAsync<GetAllSapIssueForProductionResponse>(Constants.SapApiUrls.CreateInventoryGenExits);
        }

    }
}