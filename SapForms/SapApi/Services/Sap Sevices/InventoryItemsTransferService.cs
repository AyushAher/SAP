using SapApi.Services.Helpers;
using SapApi.Modals;
using SapApi.Modals.Responses;
using SapApi.Modals.Responses.Sap;

namespace SapApi.Services
{
    public class InventoryItemsTransferService(IHttpRequestHandler requestHandler)
    {
        public Task<SapInventoryTransferRequestResponse?> CreateRequest(SapInventoryTransferRequestsRequest data)
        {
            return requestHandler.PostAsync<SapInventoryTransferRequestsRequest, SapInventoryTransferRequestResponse>(Constants.SapApiUrls.SapInventoryTransferRequests, data);
        }
        public Task<SapInventoryTransferRequestResponse?> UpdateRequest(SapInventoryTransferRequestsRequest data, string docEntry)
        {
            return requestHandler.PatchAsync<SapInventoryTransferRequestsRequest, SapInventoryTransferRequestResponse>(Constants.SapApiUrls.SapInventoryTransferRequests + docEntry, data);
        }

        public Task<SapInventoryTransferRequestListResponse?> GetAllInventoryTransferRequests()
        {
            return requestHandler.GetAsync<SapInventoryTransferRequestListResponse>(Constants.SapApiUrls.SapInventoryTransferRequests);
        }

        public Task<SapInventoryTransferRequestResponse?> GetInventoryTransferRequests(string id)
        {
            return requestHandler.GetAsync<SapInventoryTransferRequestResponse>($"{Constants.SapApiUrls.SapInventoryTransferRequests}({id})");
        }

        public async Task<List<ItemsResponse>?> GetAllItems()
        {
            SapItemsResponse? response = await requestHandler.GetAsync<SapItemsResponse>(Constants.SapApiUrls.GetAllItems);
            return response?.Value ?? [];
        }

        public async Task<List<WarehouseResponse>?> GetAllWarehouses()
        {
            SapWarehousesResponse? response = await requestHandler.GetAsync<SapWarehousesResponse>(Constants.SapApiUrls.GetAllWarehouses);
            return response?.Value;
        }

        public async Task CloseTransferRequest(string docEntry)
        {
            await requestHandler.PostAsync<dynamic, SapWarehousesResponse>(Constants.SapApiUrls.SapInventoryTransferRequestsClose(docEntry), null);
        }

        public async Task CancelTransferRequest(string docEntry)
        {
            await requestHandler.PostAsync<dynamic, SapWarehousesResponse>(Constants.SapApiUrls.SapInventoryTransferRequestsCancel(docEntry), null);
        }
    }
}