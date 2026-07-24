using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Sap;
using SapApi.Shared;
using SapApi.Domain.Entities;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Infrastructure.Services.Sap
{
    public class SapInventoryGenExitsService(IHttpRequestHandler httpRequestHandler, ApprovalService approvalService)
    {
        public async Task<SapBaseResponse?> CreateAsync(SapInventoryGenExitRequestOrderRequest request, int? policyRequestId = null)
        {
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, request, SapApi.Shared.Enums.ApprovalDocumentType.IssueForProduction, ApprovalAction.Create);
            if (policyApproval.PendingApproval)
            {
                return new SapBaseResponse
                {
                    PendingApproval = true,
                    PendingApprovalRequestId = policyApproval.PendingApprovalRequestId,
                };
            }

            return await httpRequestHandler.PostAsync<SapInventoryGenExitRequestOrderRequest, SapBaseResponse>(Constants.SapApiUrls.CreateInventoryGenExits, request);
        }

        public async Task<PaginationResponse<List<SapIssueForProductionResponse>>> GetAllPaginated(PaginationRequest request)
        {
            var sapQueries = SapPaginationBuilder.ToSapQueries(request, SapPaginationProfiles.InventoryGenExits);
            var response = await httpRequestHandler.GetAsync<GetAllSapIssueForProductionResponse>(
                Constants.SapApiUrls.CreateInventoryGenExits + sapQueries.GetQueryValue());
            var items = response?.Value ?? [];
            var totalCount = response is null
                ? 0
                : SapPaginationBuilder.ResolveTotalCount(response, items, request);
            return PaginationResponseFactory.Create(request, items, totalCount);
        }

    }
}