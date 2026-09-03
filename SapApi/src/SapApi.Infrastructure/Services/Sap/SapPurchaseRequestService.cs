using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Sap;
using SapApi.Infrastructure.Services.PurchaseRequests;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Infrastructure.Services.Sap
{
    public class SapPurchaseRequestService(
        IHttpRequestHandler requestHandler,
        ApprovalService approvalService,
        PurchaseRequestLocalStore localStore,
        SapDocumentSeriesService documentSeriesService,
        SapMasterDataService masterDataService)
    {
        public Task<GetAllSapPurchaseRequestsResponse?> GetAllPurchaseRequests(SapQueries? sapQueries = null)
        {
            sapQueries ??= SapPaginationBuilder.ToSapQueries(
                new PaginationRequest { PageNumber = 1, PageSize = 20 },
                SapPaginationProfiles.PurchaseRequests);

            return GetAllPurchaseRequestsInternal(sapQueries);
        }

        public Task<PaginationResponse<List<SapPurchaseRequestsResponse>>> GetAllPurchaseRequestsPaginated(
            PaginationRequest request,
            CancellationToken cancellationToken = default) =>
            localStore.ListFromDbAsync(request, cancellationToken);

        private Task<GetAllSapPurchaseRequestsResponse?> GetAllPurchaseRequestsInternal(SapQueries sapQueries) =>
            requestHandler.GetAsync<GetAllSapPurchaseRequestsResponse>(
                Constants.SapApiUrls.GetAllSapPurchaseRequests + sapQueries.GetQueryValue());

        public async Task<SapPurchaseRequestsResponse?> GetPurchaseRequests(
            string id,
            SapQueries? sapQueries = null,
            CancellationToken cancellationToken = default)
        {
            if (!int.TryParse(id, out var docEntry))
                return null;

            var fromDb = await localStore.GetFromDbAsync(docEntry, includeLines: true, cancellationToken);
            if (fromDb is not null)
            {
                var sapDetail = await requestHandler.GetAsync<SapPurchaseRequestsResponse>(
                    Constants.SapApiUrls.UpdateSapPurchaseRequests(docEntry),
                    cancellationToken: cancellationToken);
                SapPurchaseRequestPayloadBuilder.MergeDocumentSpecialLinesFromSap(fromDb, sapDetail);
                SapPurchaseRequestPayloadBuilder.OmitHiddenUdfDefaultsFromClientResponse(fromDb);
                return fromDb;
            }

            // Not synced yet — fetch once from SAP and persist for subsequent reads.
            var fromSap = await requestHandler.GetAsync<SapPurchaseRequestsResponse>(
                Constants.SapApiUrls.GetAllSapPurchaseRequests + $"({id})" + (sapQueries?.GetQueryValue() ?? ""),
                cancellationToken: cancellationToken);
            if (fromSap?.DocEntry is not null)
                await localStore.UpsertFromSapAsync(fromSap, cancellationToken);
            SapPurchaseRequestPayloadBuilder.OmitHiddenUdfDefaultsFromClientResponse(fromSap);
            return fromSap;
        }

        public async Task<SapPurchaseRequestsResponse?> CancelPurchaseRequest(
            int docEntry,
            CancellationToken cancellationToken = default)
        {
            await requestHandler.PostAsync<object, object>(
                Constants.SapApiUrls.CancelSapPurchaseRequests(docEntry),
                data: null!,
                cancellationToken);
            return await GetPurchaseRequests(docEntry.ToString(), cancellationToken: cancellationToken)
                   ?? throw new ApiErrorException(
                       BaseErrorCodes.ValidationFailed,
                       $"Purchase request {docEntry} was cancelled in SAP but could not be reloaded.");
        }

        public async Task<SapPurchaseRequestsResponse?> CreatePurchaseRequest(SapPurchaseRequestsResponse data, int? policyRequestId = null)
        {
            var payload = SapPurchaseRequestPayloadBuilder.Prepare(data, isUpdate: false);
            await ApplyWarehouseLocationsAsync(data, payload);
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, payload, ApprovalDocumentType.PurchaseRequest, ApprovalAction.Create);
            if (policyApproval.PendingApproval)
            {
                return new SapPurchaseRequestsResponse
                {
                    PendingApproval = true,
                    PendingApprovalRequestId = policyApproval.PendingApprovalRequestId,
                };
            }

            // Resolve OPOR Series for BPL + DocDate FY before POST — missing series surfaces as ODBC -2028.
            await documentSeriesService.EnsurePurchaseRequestSeriesAsync(payload);

            var created = await requestHandler.PostAsync<SapPurchaseRequestsResponse, SapPurchaseRequestsResponse>(
                Constants.SapApiUrls.GetAllSapPurchaseRequests, payload);
            if (created?.DocEntry is not null)
            {
                // Re-fetch full document so lines/UDFs/totals match SAP, then persist.
                var detail = await requestHandler.GetOrThrowAsync<SapPurchaseRequestsResponse>(
                    Constants.SapApiUrls.UpdateSapPurchaseRequests(created.DocEntry));
                if (detail?.DocEntry is not null)
                    await localStore.UpsertFromSapAsync(detail);
                else
                    await localStore.UpsertFromSapAsync(created);
            }

            SapPurchaseRequestPayloadBuilder.OmitHiddenUdfDefaultsFromClientResponse(created);
            return created;
        }

        public async Task<SapPurchaseRequestsResponse?> UpdatePurchaseRequest(SapPurchaseRequestsResponse data, int? policyRequestId = null)
        {
            var payload = SapPurchaseRequestPayloadBuilder.Prepare(data, isUpdate: true);
            await ApplyWarehouseLocationsAsync(data, payload);
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, payload, ApprovalDocumentType.PurchaseRequest, ApprovalAction.Update);
            if (policyApproval.PendingApproval)
            {
                return new SapPurchaseRequestsResponse
                {
                    PendingApproval = true,
                    PendingApprovalRequestId = policyApproval.PendingApprovalRequestId,
                };
            }

            // PUT is rejected on this company DB (Invalid value DocumentLines.GrossBuyPrice).
            // PATCH + B1S-ReplaceCollectionsOnPatch replaces lines the same way PUT would, so
            // deleted rows do not stay on the SAP document.
            var updated = await requestHandler.PatchAsync<SapPurchaseRequestsResponse, SapPurchaseRequestsResponse>(
                Constants.SapApiUrls.UpdateSapPurchaseRequests(payload.DocEntry),
                payload,
                new Dictionary<string, string>
                {
                    [Constants.SapServiceLayerHeaders.ReplaceCollectionsOnPatch] = "true",
                });
            if (payload.DocEntry is not null)
            {
                var detail = await requestHandler.GetOrThrowAsync<SapPurchaseRequestsResponse>(
                    Constants.SapApiUrls.UpdateSapPurchaseRequests(payload.DocEntry));
                if (detail?.DocEntry is not null)
                    await localStore.UpsertFromSapAsync(detail);
                else if (updated?.DocEntry is not null)
                    await localStore.UpsertFromSapAsync(updated);
            }

            var result = updated ?? data;
            SapPurchaseRequestPayloadBuilder.OmitHiddenUdfDefaultsFromClientResponse(result);
            return result;
        }

        public Task<PurchaseRequestSyncResult> SyncNewFromSapAsync(int? afterDocEntry = null, CancellationToken cancellationToken = default) =>
            localStore.SyncNewFromSapAsync(afterDocEntry, cancellationToken);

        public Task<PurchaseRequestSyncResult> SyncAllFromSapAsync(int? afterDocEntry = null, CancellationToken cancellationToken = default) =>
            localStore.SyncAllFromSapAsync(afterDocEntry, cancellationToken);

        public Task<PurchaseRequestSyncResult> SyncOneFromSapAsync(int docEntry, CancellationToken cancellationToken = default) =>
            localStore.SyncOneFromSapAsync(docEntry, cancellationToken);

        public Task<PurchaseRequestSyncResult?> GetSyncStateAsync(CancellationToken cancellationToken = default) =>
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
            SapPurchaseRequestsResponse source,
            SapPurchaseRequestsResponse payload)
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

            if (IsServiceDocument(payload.DocType))
                SapPurchaseRequestPayloadBuilder.CopyLocationCodeOntoLinesMissingIt(payload.DocumentLines);
        }

        private static bool IsServiceDocument(string? docType) =>
            string.Equals(docType, Constants.PurchaseOrderDocType.Document_Service, StringComparison.OrdinalIgnoreCase);

        private static string? NullIfWhiteSpace(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
