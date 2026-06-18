using SapForm.Services.Helpers;
using Shared;
using Shared.Entities;
using Shared.Requests;
using Shared.Responses.Sap;

namespace SapForm.Services
{
    public class SapPurchaseOrderService(IHttpRequestHandler requestHandler, ApprovalService approvalService)
    {
        public Task<GetAllSapPurchaseOrdersResponse?> GetAllPurchaseOrders(SapQueries? sapQueries = null)
        {
            sapQueries ??= new SapQueries
            {
                Filter = "DocDate ge '2026-01-01'"
            };
            return requestHandler.GetAsync<GetAllSapPurchaseOrdersResponse>(Constants.SapApiUrls.GetAllSapPurchaseOrders + (sapQueries?.GetQueryValue() ?? ""));
        }

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
