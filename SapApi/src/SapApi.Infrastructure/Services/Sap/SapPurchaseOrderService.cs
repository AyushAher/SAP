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
        PurchaseOrderLocalStore localStore,
        SapDocumentSeriesService documentSeriesService,
        SapMasterDataService masterDataService)
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

        /// <summary>
        /// Payment screen header/summary data — local Postgres only (no SAP fallback).
        /// Sync the PO from the Purchase Order list first if it is missing locally.
        /// </summary>
        public async Task<SapPurchaseOrdersResponse?> GetPurchaseOrderForPaymentPage(
            string id,
            CancellationToken cancellationToken = default)
        {
            if (!int.TryParse(id, out var docEntry))
                return null;

            return await localStore.GetFromDbAsync(docEntry, includeLines: false, cancellationToken);
        }

        /// <summary>
        /// Payment create/update operations — local Postgres only, including document lines
        /// required to post AP Down Payment Requests against the PO.
        /// </summary>
        public async Task<SapPurchaseOrdersResponse?> GetPurchaseOrderForPaymentOperations(
            string id,
            CancellationToken cancellationToken = default)
        {
            if (!int.TryParse(id, out var docEntry))
                return null;

            return await localStore.GetFromDbAsync(docEntry, includeLines: true, cancellationToken);
        }

        public Task<SapPurchaseOrdersResponse?> CreateGrpo(SapPurchaseOrdersResponse data)
        {
            return requestHandler.PostAsync<SapPurchaseOrdersResponse, SapPurchaseOrdersResponse>(
                Constants.SapApiUrls.PurchaseDeliveryNotes, data);
        }

        public async Task<SapPurchaseOrdersResponse?> CreatePurchaseOrder(SapPurchaseOrdersResponse data, int? policyRequestId = null)
        {
            var payload = SapPurchaseOrderPayloadBuilder.Prepare(data, isUpdate: false);
            await ApplyWarehouseLocationsAsync(data, payload);
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, payload, ApprovalDocumentType.PurchaseOrder, ApprovalAction.Create);
            if (policyApproval.PendingApproval)
            {
                return new SapPurchaseOrdersResponse
                {
                    PendingApproval = true,
                    PendingApprovalRequestId = policyApproval.PendingApprovalRequestId,
                };
            }

            // Resolve OPOR Series for BPL + DocDate FY before POST — missing series surfaces as ODBC -2028.
            await documentSeriesService.EnsurePurchaseOrderSeriesAsync(payload);

            var created = await requestHandler.PostAsync<SapPurchaseOrdersResponse, SapPurchaseOrdersResponse>(
                Constants.SapApiUrls.GetAllSapPurchaseOrders, payload);
            if (created?.DocEntry is not null)
            {
                // Re-fetch full document so lines/UDFs/totals match SAP, then persist.
                var detail = await requestHandler.GetOrThrowAsync<SapPurchaseOrdersResponse>(
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
            await ApplyWarehouseLocationsAsync(data, payload);
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, payload, ApprovalDocumentType.PurchaseOrder, ApprovalAction.Update);
            if (policyApproval.PendingApproval)
            {
                return new SapPurchaseOrdersResponse
                {
                    PendingApproval = true,
                    PendingApprovalRequestId = policyApproval.PendingApprovalRequestId,
                };
            }

            // PUT replaces DocumentLines / DocumentSpecialLines. PATCH merges and keeps
            // omitted rows, so deleted item lines would stay on the SAP document.
            var updated = await requestHandler.PutAsync<SapPurchaseOrdersResponse, SapPurchaseOrdersResponse>(
                Constants.SapApiUrls.UpdateSapPurchaseOrders(payload.DocEntry), payload);
            if (payload.DocEntry is not null)
            {
                var detail = await requestHandler.GetOrThrowAsync<SapPurchaseOrdersResponse>(
                    Constants.SapApiUrls.UpdateSapPurchaseOrders(payload.DocEntry));
                if (detail?.DocEntry is not null)
                    await localStore.UpsertFromSapAsync(detail);
                else if (updated?.DocEntry is not null)
                    await localStore.UpsertFromSapAsync(updated);
            }

            return updated ?? data;
        }

        public Task<PurchaseOrderSyncResult> SyncNewFromSapAsync(int? afterDocEntry = null, CancellationToken cancellationToken = default) =>
            localStore.SyncNewFromSapAsync(afterDocEntry, cancellationToken);

        public Task<PurchaseOrderSyncResult> SyncAllFromSapAsync(int? afterDocEntry = null, CancellationToken cancellationToken = default) =>
            localStore.SyncAllFromSapAsync(afterDocEntry, cancellationToken);

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

        /// <summary>
        /// SAP PO "Loc." is DocumentLines.LocationCode (OWHS.Location / OLCT). Fill it from the
        /// line warehouse or header warehouse when the client omitted it so the column is not blank.
        /// Must run against the original request (before Prepare strips service WarehouseCode / U_Warehouse).
        /// </summary>
        private async Task ApplyWarehouseLocationsAsync(
            SapPurchaseOrdersResponse source,
            SapPurchaseOrdersResponse payload)
        {
            if (payload.DocumentLines is not { Count: > 0 })
                return;

            var sourceLines = source.DocumentLines ?? [];
            var headerWarehouse = NullIfWhiteSpace(source.UWarehouse);

            for (var i = 0; i < payload.DocumentLines.Count; i++)
            {
                var line = payload.DocumentLines[i];
                if (line.LocationCode is > 0)
                    continue;

                var sourceLine = i < sourceLines.Count ? sourceLines[i] : null;
                var warehouseCode = NullIfWhiteSpace(sourceLine?.WarehouseCode)
                    ?? NullIfWhiteSpace(line.WarehouseCode)
                    ?? headerWarehouse;
                if (warehouseCode is null)
                    continue;

                var warehouse = await masterDataService.GetWarehouseByCodeAsync(warehouseCode);
                if (warehouse?.Location is > 0)
                    line.LocationCode = warehouse.Location;
            }
        }

        private static string? NullIfWhiteSpace(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
