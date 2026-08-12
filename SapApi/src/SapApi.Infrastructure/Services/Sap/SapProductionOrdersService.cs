using SapApi.Shared.Enums;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services.ProductionOrders;
using SapApi.Shared;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Infrastructure.Services.Sap
{
    /// <summary>
    /// Production order access for the portal. Reads come from the local mirror
    /// (<see cref="ProductionOrderLocalStore"/>); SAP is only contacted to sync or to write.
    /// </summary>
    public class SapProductionOrdersService(
        IHttpRequestHandler httpRequestHandler,
        ApprovalService approvalService,
        ProductionOrderLocalStore localStore)
    {
        public Task<PaginationResponse<List<SapProductionOrdersResponse>>> GetAllProductionOrdersPaginated(
            PaginationRequest request,
            CancellationToken cancellationToken = default) =>
            localStore.ListFromDbAsync(request, cancellationToken);

        public Task<List<SapProductionOrderLines>> GetProductionOrderLines(
            string docEntry,
            CancellationToken cancellationToken = default) =>
            int.TryParse(docEntry, out var absoluteEntry)
                ? localStore.GetLinesFromDbAsync(absoluteEntry, cancellationToken)
                : Task.FromResult(new List<SapProductionOrderLines>());

        /// <summary>
        /// Reads one production order from the mirror. An order that has never been synced (created
        /// in SAP since the last run) is pulled once and persisted, so the next read is local too.
        /// </summary>
        public async Task<SapProductionOrdersResponse?> GetProductionOrders(
            string id,
            bool checkCache = false,
            CancellationToken cancellationToken = default)
        {
            _ = checkCache;
            if (!int.TryParse(id, out var absoluteEntry) || absoluteEntry <= 0)
                return null;

            var fromDb = await localStore.GetFromDbAsync(absoluteEntry, includeLines: true, cancellationToken);
            if (fromDb is not null)
                return fromDb;

            await localStore.SyncOneFromSapAsync(absoluteEntry, cancellationToken);
            return await localStore.GetFromDbAsync(absoluteEntry, includeLines: true, cancellationToken);
        }

        public Task<ProductionOrderSyncResult> SyncNewFromSapAsync(
            int? afterAbsoluteEntry = null,
            CancellationToken cancellationToken = default) =>
            localStore.SyncNewFromSapAsync(afterAbsoluteEntry, cancellationToken);

        public Task<ProductionOrderSyncResult> SyncAllFromSapAsync(
            int? afterAbsoluteEntry = null,
            CancellationToken cancellationToken = default) =>
            localStore.SyncAllFromSapAsync(afterAbsoluteEntry, cancellationToken);

        public Task<ProductionOrderSyncResult> SyncOneFromSapAsync(
            int absoluteEntry,
            CancellationToken cancellationToken = default) =>
            localStore.SyncOneFromSapAsync(absoluteEntry, cancellationToken);

        public Task<ProductionOrderSyncResult?> GetSyncStateAsync(CancellationToken cancellationToken = default) =>
            localStore.GetSyncStateAsync(cancellationToken);

        public async Task<SapProductionOrdersResponse?> UpdateProductionOrderAsync(
            SapProductionOrdersResponse addedLines,
            int? policyRequestId = null,
            CancellationToken cancellationToken = default)
        {
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(
                policyRequestId,
                addedLines,
                ApprovalDocumentType.ProductionOrder,
                ApprovalAction.Update);
            if (policyApproval.PendingApproval)
            {
                return new SapProductionOrdersResponse
                {
                    PendingApproval = true,
                    PendingApprovalRequestId = policyApproval.PendingApprovalRequestId,
                };
            }

            var payload = PrepareProductionOrderForSap(addedLines);
            var updated = await httpRequestHandler.PutAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                Constants.SapApiUrls.GetProductionOrders(payload.AbsoluteEntry?.ToString() ?? "0"), payload);

            await RefreshMirrorAfterWriteAsync(payload.AbsoluteEntry, updated, cancellationToken);
            return updated;
        }

        public async Task<SapProductionOrdersResponse?> CreateProductionOrderAsync(
            SapProductionOrdersResponse addedLines,
            int? policyRequestId = null,
            CancellationToken cancellationToken = default)
        {
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(
                policyRequestId,
                addedLines,
                ApprovalDocumentType.ProductionOrder,
                ApprovalAction.Create);
            if (policyApproval.PendingApproval)
            {
                return new SapProductionOrdersResponse
                {
                    PendingApproval = true,
                    PendingApprovalRequestId = policyApproval.PendingApprovalRequestId,
                };
            }

            var payload = PrepareProductionOrderForSap(addedLines);
            var created = await httpRequestHandler.PostAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                Constants.SapApiUrls.CreateProductionOrder, payload);

            await RefreshMirrorAfterWriteAsync(created?.AbsoluteEntry, created, cancellationToken);
            return created;
        }

        /// <summary>
        /// Keeps the read model consistent immediately after a write so the list does not show a
        /// stale row until the next sync. A mirror failure must not fail the SAP write.
        /// </summary>
        private async Task RefreshMirrorAfterWriteAsync(
            int? absoluteEntry,
            SapProductionOrdersResponse? sapResponse,
            CancellationToken cancellationToken)
        {
            if (absoluteEntry is null or <= 0 || sapResponse?.Error is not null)
                return;

            try
            {
                await localStore.SyncOneFromSapAsync(absoluteEntry.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(
                    ex,
                    "Could not refresh the production order mirror for {AbsoluteEntry} after a SAP write",
                    absoluteEntry);
            }
        }

        /// <summary>
        /// Both writes go through this: an approved request is replayed through the create path, so a
        /// body SAP would reject must not survive there either.
        /// </summary>
        static SapProductionOrdersResponse PrepareProductionOrderForSap(SapProductionOrdersResponse order)
        {
            order.ProductionOrderLines = order.ProductionOrderLines?
                .Select((line, index) =>
                {
                    line.VisualOrder = index;
                    line.DocumentAbsoluteEntry = order.AbsoluteEntry;
                    line.SerialNumbers = null;
                    line.BatchNumbers = null;
                    // ProductionOrderLine.UoMCode must be a whole number (UoM entry). Drop inventory UoM names like "KG".
                    line.UoMCode = SapProductionOrderUoMNormalizer.NormalizeUoMCode(line.UoMCode);
                    return line;
                })
                .ToList() ?? [];

            // Project/customer names are display-only values resolved from master data. They map to UDFs
            // that do not exist on ProductionOrders in every company DB, and SAP rejects unknown
            // properties outright ("Property 'U_CustomerName' of 'ProductionOrder' is invalid").
            order.ProjectName = null;
            order.CustomerName = null;

            order.ProductionOrdersSalesOrderLines = null;
            order.ProductionOrdersStages = null;
            order.ProductionOrdersDocumentReferences = null;
            order.ODataMetadata = null;
            order.ODataNextLink = null;
            order.Error = null;

            return order;
        }
    }
}
