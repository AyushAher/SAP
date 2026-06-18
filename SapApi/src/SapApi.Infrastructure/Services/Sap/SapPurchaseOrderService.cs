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
    public class SapPurchaseOrderService(IHttpRequestHandler requestHandler, ApprovalService approvalService)
    {
        public Task<GetAllSapPurchaseOrdersResponse?> GetAllPurchaseOrders(SapQueries? sapQueries = null)
        {
            sapQueries ??= SapPaginationBuilder.ToSapQueries(
                new PaginationRequest { PageNumber = 1, PageSize = 20 },
                SapPaginationProfiles.PurchaseOrders);

            return GetAllPurchaseOrdersInternal(sapQueries);
        }

        public async Task<PaginationResponse<List<SapPurchaseOrdersResponse>>> GetAllPurchaseOrdersPaginated(
            PaginationRequest request)
        {
            var sapQueries = SapPaginationBuilder.ToSapQueries(request, SapPaginationProfiles.PurchaseOrders);
            var response = await GetAllPurchaseOrdersInternal(sapQueries);
            var items = response?.Value ?? [];
            var totalCount = response is null
                ? 0
                : SapPaginationBuilder.ResolveTotalCount(response, items, request);

            return PaginationResponseFactory.Create(request, items, totalCount);
        }

        private Task<GetAllSapPurchaseOrdersResponse?> GetAllPurchaseOrdersInternal(SapQueries sapQueries) =>
            requestHandler.GetAsync<GetAllSapPurchaseOrdersResponse>(
                Constants.SapApiUrls.GetAllSapPurchaseOrders + sapQueries.GetQueryValue(),
                checkCache: true);

        public Task<SapPurchaseOrdersResponse?> GetPurchaseOrders(string id, SapQueries? sapQueries = null)
        {
            return requestHandler.GetAsync<SapPurchaseOrdersResponse>(
                Constants.SapApiUrls.GetAllSapPurchaseOrders + $"({id})" + (sapQueries?.GetQueryValue() ?? ""));
        }

        public Task<SapPurchaseOrdersResponse?> CreateGrpo(SapPurchaseOrdersResponse data)
        {
            return requestHandler.PostAsync<SapPurchaseOrdersResponse, SapPurchaseOrdersResponse>(
                Constants.SapApiUrls.PurchaseDeliveryNotes, data);
        }

        public async Task<SapPurchaseOrdersResponse?> CreatePurchaseOrder(SapPurchaseOrdersResponse data, int? policyRequestId = null)
        {
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, data, ApprovalDocumentType.PurchaseOrder, ApprovalAction.Create);
            if (policyApproval.PendingApproval)
            {
                return policyApproval as SapPurchaseOrdersResponse;
            }
            var response = await requestHandler.PostAsync<SapPurchaseOrdersResponse, SapPurchaseOrdersResponse>(Constants.SapApiUrls.GetAllSapPurchaseOrders, data);

            if (response?.DocEntry is not null)
            {
                await requestHandler.PatchCachedEntityAsync<SapPurchaseOrdersResponse>("PurchaseOrders", response.DocEntry.Value, "DocEntry");
            }

            return response;

        }

        public async Task<SapPurchaseOrdersResponse?> UpdatePurchaseOrder(SapPurchaseOrdersResponse data, int? policyRequestId = null)
        {
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, data, ApprovalDocumentType.PurchaseOrder, ApprovalAction.Update);
            if (policyApproval.PendingApproval)
            {
                return policyApproval as SapPurchaseOrdersResponse;
            }
            var response = await requestHandler.PatchAsync<SapPurchaseOrdersResponse, SapPurchaseOrdersResponse>(Constants.SapApiUrls.UpdateSapPurchaseOrders(data.DocEntry), data);

            if (response?.DocEntry is not null)
            {
                await requestHandler.PatchCachedEntityAsync<SapPurchaseOrdersResponse>("PurchaseOrders", response.DocEntry.Value, "DocEntry");
            }

            return response;

        }

        public Task<SapGetAllProjectDetailsResponse?> GetAllProjectDetailsResponse()
        {
            return requestHandler.GetAsync<SapGetAllProjectDetailsResponse>(Constants.SapApiUrls.GetAllProjectDetails);
        }

        public Task<SapGetAllBranchesResponse?> GetAllBplResponse()
        {
            return requestHandler.GetAsync<SapGetAllBranchesResponse>(Constants.SapApiUrls.GetAllBpl);
        }
    }
}
