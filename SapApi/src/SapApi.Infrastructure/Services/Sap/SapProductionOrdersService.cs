using SapApi.Shared.Enums;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Sap;
using SapApi.Shared;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Infrastructure.Services.Sap
{
    public class SapProductionOrdersService(IHttpRequestHandler httpRequestHandler, ApprovalService approvalService)
    {
        public async Task<GetAllSapProductionOrdersResponse?> GetAllProductionOrders()
        {
            var sapQueries = SapPaginationBuilder.ToSapQueries(
                new PaginationRequest { PageNumber = 1, PageSize = 20 },
                SapPaginationProfiles.ProductionOrders);

            return await GetAllProductionOrdersInternal(sapQueries);
        }

        public async Task<PaginationResponse<List<SapProductionOrdersResponse>>> GetAllProductionOrdersPaginated(
            PaginationRequest request)
        {
            var sapQueries = SapPaginationBuilder.ToSapQueries(request, SapPaginationProfiles.ProductionOrders);
            var response = await GetAllProductionOrdersInternal(sapQueries);
            var items = response?.Value ?? [];
            var totalCount = response is null
                ? 0
                : SapPaginationBuilder.ResolveTotalCount(response, items, request);

            return PaginationResponseFactory.Create(request, items, totalCount);
        }

        private Task<GetAllSapProductionOrdersResponse?> GetAllProductionOrdersInternal(SapQueries sapQueries) =>
            httpRequestHandler.GetAsync<GetAllSapProductionOrdersResponse>(
                Constants.SapApiUrls.GetAllProductionOrders + sapQueries.GetQueryValue());


        public async Task<GetAllSapProductionOrderLinesResponse?> GetProductionOrderLines(string docEntry)
        {
            return await httpRequestHandler.ExecuteSqlQueryAsync<GetAllSapProductionOrderLinesResponse>(Constants.SapSqlQueryName
                .GetProductionOrderLines, new Dictionary<string, object>
                {
                    { "_docentry", docEntry }
                });
        }

        public async Task<SapProductionOrdersResponse?> GetProductionOrders(string id, bool checkCache = false)
        {
            _ = checkCache;
            return await httpRequestHandler.GetAsync<SapProductionOrdersResponse>(Constants.SapApiUrls
                .GetProductionOrders(id));
        }

        public async Task<SapProductionOrdersResponse?> UpdateProductionOrderAsync(SapProductionOrdersResponse addedLines, int? policyRequestId = null)
        {
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, addedLines, ApprovalDocumentType.ProductionOrder, ApprovalAction.Update);
            if (policyApproval.PendingApproval)
            {
                return policyApproval as SapProductionOrdersResponse;
            }

            var payload = PrepareProductionOrderForSapPut(addedLines);
            return await httpRequestHandler.PutAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                Constants.SapApiUrls.GetProductionOrders(payload.AbsoluteEntry?.ToString() ?? "0"), payload);
        }

        static SapProductionOrdersResponse PrepareProductionOrderForSapPut(SapProductionOrdersResponse order)
        {
            order.ProductionOrderLines = order.ProductionOrderLines?
                .Select((line, index) =>
                {
                    line.VisualOrder = index;
                    line.DocumentAbsoluteEntry = order.AbsoluteEntry;
                    line.SerialNumbers = null;
                    line.BatchNumbers = null;
                    return line;
                })
                .ToList() ?? [];

            order.ProductionOrdersSalesOrderLines = null;
            order.ProductionOrdersStages = null;
            order.ProductionOrdersDocumentReferences = null;
            order.ODataMetadata = null;
            order.ODataNextLink = null;
            order.Error = null;

            return order;
        }

        public async Task<SapProductionOrdersResponse?> CreateProductionOrderAsync(SapProductionOrdersResponse addedLines, int? policyRequestId = null)
        {
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, addedLines, ApprovalDocumentType.ProductionOrder, ApprovalAction.Create);
            if (policyApproval.PendingApproval)
            {
                return policyApproval as SapProductionOrdersResponse;
            }
            return await httpRequestHandler.PostAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                Constants.SapApiUrls.CreateProductionOrder, addedLines);
        }
    }
}
