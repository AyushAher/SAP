using SapForm.Services.Helpers;
using Shared;
using Shared.Entities;
using Shared.Requests;
using Shared.Responses.Sap;

namespace SapForm.Services
{
    public class SapInventoryGenExitsService(IHttpRequestHandler httpRequestHandler, ApprovalService approvalService)
    {
        public async Task<SapBaseResponse?> CreateAsync(SapInventoryGenExitRequestOrderRequest request, int? policyRequestId = null)
        {
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, request, Shared.Enums.ApprovalDocumentType.IssueForProduction, ApprovalAction.Create);
            if (policyApproval.PendingApproval)
            {
                return policyApproval as SapInventoryTransferRequestResponse;
            }

            return await httpRequestHandler.PostAsync<SapInventoryGenExitRequestOrderRequest, SapBaseResponse>(Constants.SapApiUrls.CreateInventoryGenExits, request);
        }

        public Task<GetAllSapIssueForProductionResponse?> GetAll(SapQueries? sapQueries = null)
        {
            sapQueries ??= new SapQueries
            {
                Select = "DocEntry,CardCode,CardName,Project,DownPayment,DocTotal",
            };

            return httpRequestHandler.GetAsync<GetAllSapIssueForProductionResponse>(Constants.SapApiUrls.CreateInventoryGenExits + (sapQueries?.GetQueryValue() ?? ""));
        }

    }
}