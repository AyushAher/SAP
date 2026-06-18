using Azure.Core;
using SapForm.Services.Helpers;
using Shared;
using Shared.Entities;
using Shared.Requests;
using Shared.Responses.Sap;
namespace SapForm.Services
{
    public class InventoryItemsTransferService(IHttpRequestHandler requestHandler, ApprovalService approvalService)
    {
        public async Task<SapInventoryTransferRequestResponse?> CreateRequest(SapInventoryTransferRequestsRequest data, int? policyRequestId = null)
        {
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, data, ApprovalDocumentType.InventoryItemsTransfer, ApprovalAction.Create);
            if (policyApproval.PendingApproval)
            {
                return policyApproval as SapInventoryTransferRequestResponse;
            }
            return await requestHandler.PostAsync<SapInventoryTransferRequestsRequest, SapInventoryTransferRequestResponse>(Constants.SapApiUrls.SapInventoryTransferRequests, data);
        }
        public async Task<SapInventoryTransferRequestResponse?> UpdateRequest(SapInventoryTransferRequestsRequest data, string docEntry, int? policyRequestId = null)
        {
            _ = int.TryParse(docEntry, out var dEntry);
            data.DocEntry = dEntry;
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, data, ApprovalDocumentType.InventoryItemsTransfer, ApprovalAction.Update);
            if (policyApproval.PendingApproval)
            {
                return policyApproval as SapInventoryTransferRequestResponse;
            }
            return await requestHandler.PatchAsync<SapInventoryTransferRequestsRequest, SapInventoryTransferRequestResponse>(Constants.SapApiUrls.SapInventoryTransferRequests + docEntry, data);
        }
        public Task<SapInventoryTransferRequestListResponse?> GetAllInventoryTransferRequests(SapQueries? sapQueries = null)
        {
            sapQueries ??= new SapQueries
            {
                Select = "DocEntry,DocDate,DueDate,FromWarehouse,ToWarehouse,CardCode,CardName,ContactPerson",
            };

            return requestHandler.GetAsync<SapInventoryTransferRequestListResponse>(Constants.SapApiUrls.SapInventoryTransferRequests + sapQueries.GetQueryValue());
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