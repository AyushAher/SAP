using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Sap;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Infrastructure.Services.Sap
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
            sapQueries ??= SapPaginationBuilder.ToSapQueries(
                new PaginationRequest { PageNumber = 1, PageSize = 20 },
                SapPaginationProfiles.InventoryTransfers);

            return GetAllInventoryTransferRequestsInternal(sapQueries);
        }

        public async Task<PaginationResponse<List<SapInventoryTransferRequestResponse>>> GetAllInventoryTransferRequestsPaginated(
            PaginationRequest request)
        {
            var sapQueries = SapPaginationBuilder.ToSapQueries(request, SapPaginationProfiles.InventoryTransfers);
            var response = await GetAllInventoryTransferRequestsInternal(sapQueries);
            var items = response?.Value ?? [];
            var totalCount = response is null
                ? 0
                : SapPaginationBuilder.ResolveTotalCount(response, items, request);

            return PaginationResponseFactory.Create(request, items, totalCount);
        }

        private Task<SapInventoryTransferRequestListResponse?> GetAllInventoryTransferRequestsInternal(SapQueries sapQueries) =>
            requestHandler.GetAsync<SapInventoryTransferRequestListResponse>(
                Constants.SapApiUrls.SapInventoryTransferRequests + sapQueries.GetQueryValue());

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