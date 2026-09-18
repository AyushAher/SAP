using System.Globalization;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Sap;
using SapApi.Infrastructure.Services.PurchaseRequests;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses;
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

        /// <summary>
        /// Every line of every purchase request matching the report filter dialog (username, date
        /// range, required-by range, project range, period, branch), read live from SAP rather than
        /// the local mirror — the report must reflect SAP's current state, not yesterday's sync.
        /// No $select is sent so SAP returns its full default shape (DocumentLines included), the
        /// same way the proven single-document fetch already works.
        /// </summary>
        public async Task<List<PurchaseRequestReportLineResponse>> GetReportRowsFromSapAsync(
            IReadOnlyList<FilterModel> filters,
            CancellationToken cancellationToken = default)
        {
            const int maxDocuments = 2000;
            const int pageSize = 100;

            var filter = BuildReportSapFilter(filters);
            var documents = new List<SapPurchaseRequestsResponse>();
            var skip = 0;

            while (documents.Count < maxDocuments)
            {
                var top = Math.Min(pageSize, maxDocuments - documents.Count);
                var queries = new SapQueries
                {
                    Filter = filter,
                    OrderBy = "DocDate,DocNum",
                    Skip = skip > 0 ? skip.ToString(CultureInfo.InvariantCulture) : null,
                    Top = top.ToString(CultureInfo.InvariantCulture),
                };

                var page = await GetAllPurchaseRequestsInternal(queries);
                var rows = page?.Value ?? [];
                documents.AddRange(rows);

                if (rows.Count < top)
                    break;

                skip += rows.Count;
            }

            return MapToReportLines(documents);
        }

        private static string? BuildReportSapFilter(IReadOnlyList<FilterModel> filters)
        {
            var parts = new List<string>();

            foreach (var filter in filters)
            {
                if (filter.Value is null || string.IsNullOrWhiteSpace(filter.Value.ToString()))
                    continue;

                var term = filter.Value.ToString()!.Trim();
                var escaped = SapPaginationBuilder.EscapeODataString(term);

                if (filter.Field.Equals("Username", StringComparison.OrdinalIgnoreCase))
                {
                    // RequesterName is often blank in this tenant's data — Requester (the code the
                    // user actually types/is assigned) is the field reliably populated.
                    parts.Add($"contains(Requester,'{escaped}')");
                }
                else if (filter.Field.Equals("DocDate", StringComparison.OrdinalIgnoreCase)
                    && DateTime.TryParse(term, out var docDate))
                {
                    parts.Add(filter.Operator == "lte"
                        ? $"DocDate le '{docDate:yyyy-MM-dd}'"
                        : $"DocDate ge '{docDate:yyyy-MM-dd}'");
                }
                else if (filter.Field.Equals("RequiredDate", StringComparison.OrdinalIgnoreCase)
                    && DateTime.TryParse(term, out var requiredDate))
                {
                    parts.Add(filter.Operator == "lte"
                        ? $"RequriedDate le '{requiredDate:yyyy-MM-dd}'"
                        : $"RequriedDate ge '{requiredDate:yyyy-MM-dd}'");
                }
                else if (filter.Field.Equals("Project", StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add(filter.Operator == "lte"
                        ? $"Project le '{escaped}'"
                        : $"Project ge '{escaped}'");
                }
                else if (filter.Field.Equals("Period", StringComparison.OrdinalIgnoreCase)
                    && DateTime.TryParse($"{term}-01", out var periodStart))
                {
                    var start = new DateTime(periodStart.Year, periodStart.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    var end = start.AddMonths(1);
                    parts.Add($"DocDate ge '{start:yyyy-MM-dd}'");
                    parts.Add($"DocDate lt '{end:yyyy-MM-dd}'");
                }
                else if (filter.Field.Equals("BPLId", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(term, out var bplId))
                {
                    parts.Add($"BPLId eq {bplId}");
                }
            }

            return parts.Count > 0 ? string.Join(" and ", parts) : null;
        }

        private static List<PurchaseRequestReportLineResponse> MapToReportLines(
            IReadOnlyList<SapPurchaseRequestsResponse> documents)
        {
            var rows = new List<PurchaseRequestReportLineResponse>();
            foreach (var doc in documents)
            {
                var itemType = doc.DocType == Constants.PurchaseOrderDocType.Document_Service ? "Service" : "Item";
                var lines = (doc.DocumentLines ?? [])
                    .OrderBy(l => l.LineNum);
                foreach (var line in lines)
                {
                    rows.Add(new PurchaseRequestReportLineResponse
                    {
                        DocNum = doc.DocNum ?? doc.DocEntry ?? 0,
                        PostingDate = doc.DocDate,
                        UserName = !string.IsNullOrWhiteSpace(doc.RequesterName) ? doc.RequesterName : doc.Requester,
                        RequiredDate = line.RequiredDate ?? doc.RequiredDate,
                        ProjectCode = line.ProjectCode ?? doc.Project,
                        ItemType = itemType,
                        ItemCode = line.ItemCode,
                        Description = line.ItemDescription,
                        FreeText = line.FreeText,
                        Quantity = line.Quantity,
                        Unit = line.UoMCode,
                    });
                }
            }

            return rows;
        }

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
            // HTTP DELETE is rejected on PurchaseRequests ("The document cannot be removed").
            // Service Layer cancel is POST .../PurchaseRequests({id})/Cancel.
            await requestHandler.PostAsync<object, object>(
                Constants.SapApiUrls.CancelSapPurchaseRequests(docEntry),
                data: null!,
                cancellationToken);
            var detail = await requestHandler.GetOrThrowAsync<SapPurchaseRequestsResponse>(
                Constants.SapApiUrls.UpdateSapPurchaseRequests(docEntry),
                cancellationToken);
            if (detail?.DocEntry is not null)
                await localStore.UpsertFromSapAsync(detail, cancellationToken);
            SapPurchaseRequestPayloadBuilder.OmitHiddenUdfDefaultsFromClientResponse(detail);
            return detail
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

            // Resolve OPRQ Series for BPL + DocDate FY before POST — missing series surfaces as ODBC -2028.
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

                if (!IsServiceDocument(payload.DocType) && string.IsNullOrWhiteSpace(line.WarehouseCode))
                    line.WarehouseCode = warehouseCode;

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
