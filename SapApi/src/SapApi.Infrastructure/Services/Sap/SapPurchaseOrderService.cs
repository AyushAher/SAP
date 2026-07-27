using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Sap;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Infrastructure.Services.Sap
{
    public class SapPurchaseOrderService(
        IHttpRequestHandler requestHandler,
        ApprovalService approvalService,
        PurchaseOrderLocalStore localStore)
    {
        public Task<GetAllSapPurchaseOrdersResponse?> GetAllPurchaseOrders(SapQueries? sapQueries = null)
        {
            sapQueries ??= SapPaginationBuilder.ToSapQueries(
                new PaginationRequest { PageNumber = 1, PageSize = 20 },
                SapPaginationProfiles.PurchaseOrders);

            return GetAllPurchaseOrdersInternal(sapQueries);
        }

        public Task<PaginationResponse<List<SapPurchaseOrdersResponse>>> GetAllPurchaseOrdersPaginated(
            PaginationRequest request,
            CancellationToken cancellationToken = default) =>
            localStore.ListFromDbAsync(request, cancellationToken);

        private Task<GetAllSapPurchaseOrdersResponse?> GetAllPurchaseOrdersInternal(SapQueries sapQueries) =>
            requestHandler.GetAsync<GetAllSapPurchaseOrdersResponse>(
                Constants.SapApiUrls.GetAllSapPurchaseOrders + sapQueries.GetQueryValue());

        public async Task<SapPurchaseOrdersResponse?> GetPurchaseOrders(
            string id,
            SapQueries? sapQueries = null,
            CancellationToken cancellationToken = default)
        {
            if (!int.TryParse(id, out var docEntry))
                return null;

            var fromDb = await localStore.GetFromDbAsync(docEntry, includeLines: true, cancellationToken);
            if (fromDb is not null)
                return fromDb;

            // Not synced yet — fetch once from SAP and persist for subsequent reads.
            var fromSap = await requestHandler.GetAsync<SapPurchaseOrdersResponse>(
                Constants.SapApiUrls.GetAllSapPurchaseOrders + $"({id})" + (sapQueries?.GetQueryValue() ?? ""),
                cancellationToken: cancellationToken);
            if (fromSap?.DocEntry is not null)
                await localStore.UpsertFromSapAsync(fromSap, cancellationToken);
            return fromSap;
        }

        public Task<SapPurchaseOrdersResponse?> GetPurchaseOrderForPaymentPage(string id, CancellationToken cancellationToken = default) =>
            GetPurchaseOrderFromDbOrSapAsync(id, includeLines: false, cancellationToken);

        public Task<SapPurchaseOrdersResponse?> GetPurchaseOrderForPaymentOperations(string id, CancellationToken cancellationToken = default) =>
            GetPurchaseOrderFromDbOrSapAsync(id, includeLines: true, cancellationToken);

        private async Task<SapPurchaseOrdersResponse?> GetPurchaseOrderFromDbOrSapAsync(
            string id,
            bool includeLines,
            CancellationToken cancellationToken)
        {
            if (!int.TryParse(id, out var docEntry))
                return null;

            var fromDb = await localStore.GetFromDbAsync(docEntry, includeLines, cancellationToken);
            if (fromDb is not null)
                return fromDb;

            var fromSap = await requestHandler.GetAsync<SapPurchaseOrdersResponse>(
                Constants.SapApiUrls.GetAllSapPurchaseOrders + $"({id})",
                cancellationToken: cancellationToken);
            if (fromSap?.DocEntry is not null)
                await localStore.UpsertFromSapAsync(fromSap, cancellationToken);
            return fromSap;
        }

        public Task<SapPurchaseOrdersResponse?> CreateGrpo(SapPurchaseOrdersResponse data)
        {
            return requestHandler.PostAsync<SapPurchaseOrdersResponse, SapPurchaseOrdersResponse>(
                Constants.SapApiUrls.PurchaseDeliveryNotes, data);
        }

        public async Task<SapPurchaseOrdersResponse?> CreatePurchaseOrder(SapPurchaseOrdersResponse data, int? policyRequestId = null)
        {
            var payload = SapPurchaseOrderPayloadBuilder.Prepare(data, isUpdate: false);
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, payload, ApprovalDocumentType.PurchaseOrder, ApprovalAction.Create);
            if (policyApproval.PendingApproval)
            {
                return new SapPurchaseOrdersResponse
                {
                    PendingApproval = true,
                    PendingApprovalRequestId = policyApproval.PendingApprovalRequestId,
                };
            }

            var created = await requestHandler.PostAsync<SapPurchaseOrdersResponse, SapPurchaseOrdersResponse>(
                Constants.SapApiUrls.GetAllSapPurchaseOrders, payload);
            if (created?.DocEntry is not null)
            {
                // Re-fetch full document so lines/UDFs match SAP, then persist.
                var detail = await requestHandler.GetAsync<SapPurchaseOrdersResponse>(
                    Constants.SapApiUrls.UpdateSapPurchaseOrders(created.DocEntry));
                if (detail?.DocEntry is not null)
                    await localStore.UpsertFromSapAsync(detail);
                else
                    await localStore.UpsertFromSapAsync(created);
            }

            return created;
        }

        public async Task<SapPurchaseOrdersResponse?> UpdatePurchaseOrder(SapPurchaseOrdersResponse data, int? policyRequestId = null)
        {
            var payload = SapPurchaseOrderPayloadBuilder.Prepare(data, isUpdate: true);
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, payload, ApprovalDocumentType.PurchaseOrder, ApprovalAction.Update);
            if (policyApproval.PendingApproval)
            {
                return new SapPurchaseOrdersResponse
                {
                    PendingApproval = true,
                    PendingApprovalRequestId = policyApproval.PendingApprovalRequestId,
                };
            }

            var updated = await requestHandler.PatchAsync<SapPurchaseOrdersResponse, SapPurchaseOrdersResponse>(
                Constants.SapApiUrls.UpdateSapPurchaseOrders(payload.DocEntry), payload);
            if (payload.DocEntry is not null)
            {
                var detail = await requestHandler.GetAsync<SapPurchaseOrdersResponse>(
                    Constants.SapApiUrls.UpdateSapPurchaseOrders(payload.DocEntry));
                if (detail?.DocEntry is not null)
                    await localStore.UpsertFromSapAsync(detail);
            }

            return updated ?? data;
        }

        public Task<PurchaseOrderSyncResult> SyncNewFromSapAsync(CancellationToken cancellationToken = default) =>
            localStore.SyncNewFromSapAsync(cancellationToken);

        public Task<PurchaseOrderSyncResult> SyncAllFromSapAsync(CancellationToken cancellationToken = default) =>
            localStore.SyncAllFromSapAsync(cancellationToken);

        public Task<PurchaseOrderSyncResult> SyncOneFromSapAsync(int docEntry, CancellationToken cancellationToken = default) =>
            localStore.SyncOneFromSapAsync(docEntry, cancellationToken);

        public Task<PurchaseOrderSyncResult?> GetSyncStateAsync(CancellationToken cancellationToken = default) =>
            localStore.GetSyncStateAsync(cancellationToken);

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
