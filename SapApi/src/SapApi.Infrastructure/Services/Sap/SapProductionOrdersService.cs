using SapApi.Shared.Enums;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services.ProductionOrders;
using SapApi.Shared;
using SapApi.Shared.Exceptions;
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
            bool excludeSubassemblies = true,
            CancellationToken cancellationToken = default) =>
            localStore.ListFromDbAsync(request, excludeSubassemblies, cancellationToken);

        public Task<List<SapProductionOrdersResponse>?> ListSubassembliesAsync(
            int parentAbsoluteEntry,
            bool includeCancelled = false,
            CancellationToken cancellationToken = default) =>
            localStore.ListSubassembliesAsync(parentAbsoluteEntry, includeCancelled, cancellationToken);

        public async Task<SapProductionOrdersResponse?> CancelProductionOrderAsync(
            int absoluteEntry,
            int? policyRequestId = null,
            CancellationToken cancellationToken = default)
        {
            var order = await GetProductionOrders(absoluteEntry.ToString(), cancellationToken: cancellationToken);
            if (order is null)
                return null;

            order.Status = Constants.SapProductionOrderStatus.Cancelled;
            return await UpdateProductionOrderAsync(order, policyRequestId, cancellationToken);
        }

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
            if (!int.TryParse(id, out var absoluteEntry) || absoluteEntry == 0)
                return null;

            if (absoluteEntry < 0)
                return await localStore.GetFromDbAsync(absoluteEntry, includeLines: true, cancellationToken);

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

        /// <summary>
        /// Placeholder parent DocumentNumber used to tag component lines on a first-time create,
        /// before SAP has assigned the real number. Unique per order (WOR1), so it does not collide.
        /// </summary>
        const string DraftParentDocumentNumber = "0";

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

            if (await IsVirtualSubassemblyAsync(addedLines, cancellationToken))
                return await UpdateVirtualSubassemblyAsync(addedLines, cancellationToken);

            // PUT replaces the whole document. A header-only body that omits ItemNo / Status /
            // PostingDate is treated as clearing those fields (ODBC -1029 / invalid status /
            // missing product). Overlay the user's edits onto the live SAP order instead.
            var headerOnly = addedLines.ProductionOrderLines is not { Count: > 0 };
            addedLines = await OverlayEditableHeaderOntoLiveSapAsync(addedLines, headerOnly, cancellationToken);
            if (!headerOnly || addedLines.ProductionOrderLines is not { Count: > 0 })
                addedLines = await PreserveTaggedSubassemblyLinesAsync(addedLines, cancellationToken);
            // Never drop LineNumber on this path. Header-only overlay already has live identity;
            // new component rows are appended by omitting LineNumber in Prepare when the number
            // is not in the live collection. A full replace still happens in the virtual
            // sub-assembly merge when no issued/closed row exists.
            return await PutProductionOrderToSapAsync(
                addedLines,
                cancellationToken,
                preserveExistingLineStorage: headerOnly,
                replaceEntireLineCollection: false);
        }

        /// <summary>
        /// Appends one manual production order line via PATCH. Used by Issue for Production add-line,
        /// not by the Production Order screen (which uses <see cref="UpdateProductionOrderAsync"/>).
        /// LineNumber / VisualOrder are omitted so Service Layer appends instead of updating an
        /// existing line (SAP error 254000224: Item code cannot be changed).
        /// PostingDate is omitted: Live blocks posting-date edits even when the value is unchanged.
        /// </summary>
        public async Task<SapProductionOrdersResponse?> PatchProductionOrderLineAsync(
            int absoluteEntry,
            SapProductionOrderLines line,
            DateTime? postingDate = null,
            CancellationToken cancellationToken = default)
        {
            _ = postingDate;
            var preparedLine = PrepareLineForSapPatch(line);
            var patchBody = new SapProductionOrderLinePatchRequest
            {
                ProductionOrderLines = [preparedLine],
            };

            var patched = await httpRequestHandler.PatchAsync<SapProductionOrderLinePatchRequest, SapProductionOrdersResponse>(
                Constants.SapApiUrls.GetProductionOrders(absoluteEntry.ToString()),
                patchBody,
                cancellationToken);

            await RefreshMirrorAfterWriteAsync(absoluteEntry, patched, cancellationToken);

            if (patched?.Error is not null)
                return patched;

            return await localStore.GetFromDbAsync(absoluteEntry, includeLines: true, cancellationToken) ?? patched;
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

            var parentNo = NullIfBlank(addedLines.ParentProductionOrderNo);
            if (parentNo is not null)
                return await CreateVirtualSubassemblyAsync(addedLines, cancellationToken);

            var children = (addedLines.Subassemblies ?? [])
                .Where(child => (child.ProductionOrderLines ?? [])
                    .Any(line => !string.IsNullOrWhiteSpace(line.ItemNo)))
                .ToList();
            addedLines.Subassemblies = null;
            if (children.Count > 0)
                return await CreateParentWithSubassembliesAsync(addedLines, children, cancellationToken);

            var payload = PrepareProductionOrderForSap(addedLines);
            var created = await httpRequestHandler.PostAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                Constants.SapApiUrls.CreateProductionOrder, payload);

            await RefreshMirrorAfterWriteAsync(created?.AbsoluteEntry, created, cancellationToken);
            return created;
        }

        async Task<SapProductionOrdersResponse?> PutProductionOrderToSapAsync(
            SapProductionOrdersResponse addedLines,
            CancellationToken cancellationToken,
            bool preserveExistingLineStorage = false,
            bool replaceEntireLineCollection = false)
        {
            var parentNo = NullIfBlank(addedLines.ParentProductionOrderNo);
            HashSet<int>? existingLineNumbers = null;
            if (!replaceEntireLineCollection)
            {
                existingLineNumbers = await ExistingLineNumbersAsync(addedLines.AbsoluteEntry, cancellationToken)
                    ?? [];
                // Live issued/closed rows may exist in SAP but not yet in the mirror. Trust
                // LineNumbers already on that payload so they are not treated as new appends.
                if (HasProtectedLines(addedLines.ProductionOrderLines))
                {
                    foreach (var number in addedLines.ProductionOrderLines!
                        .Where(line => line.LineNumber is not null)
                        .Select(line => line.LineNumber!.Value))
                    {
                        existingLineNumbers.Add(number);
                    }
                }

                if (existingLineNumbers.Count == 0)
                    existingLineNumbers = null;
            }
            var payload = PrepareProductionOrderForSap(
                addedLines,
                existingLineNumbers,
                preserveExistingLineStorage,
                replaceEntireLineCollection);
            var updated = await httpRequestHandler.PutAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                Constants.SapApiUrls.GetProductionOrders(payload.AbsoluteEntry?.ToString() ?? "0"), payload);

            await RefreshMirrorAfterWriteAsync(payload.AbsoluteEntry, updated, cancellationToken);
            await localStore.PreserveParentProductionOrderNoAsync(
                payload.AbsoluteEntry, parentNo, cancellationToken);
            return updated;
        }

        async Task<bool> IsVirtualSubassemblyAsync(
            SapProductionOrdersResponse order,
            CancellationToken cancellationToken)
        {
            if (order.AbsoluteEntry is < 0)
                return true;
            if (order.AbsoluteEntry is int abs and > 0)
                return await localStore.IsVirtualSubassemblyAsync(abs, cancellationToken);
            return NullIfBlank(order.ParentProductionOrderNo) is not null;
        }

        async Task<SapProductionOrdersResponse> CreateParentWithSubassembliesAsync(
            SapProductionOrdersResponse parent,
            List<SapProductionOrdersResponse> children,
            CancellationToken cancellationToken)
        {
            var mergedLines = new List<SapProductionOrderLines>();
            string? drawingNo = NullIfBlank(parent.DrawingNo);
            var sequence = 1;
            foreach (var child in children)
            {
                var tag = $"{DraftParentDocumentNumber}/{sequence}";
                var copies = (child.ProductionOrderLines ?? [])
                    .Where(line => !string.IsNullOrWhiteSpace(line.ItemNo))
                    .Select(CopyItemLine)
                    .ToList();
                mergedLines.AddRange(StampSubassemblyLinesForParent(
                    copies,
                    [],
                    [],
                    tag,
                    child.DrawingNo,
                    child.ProductDescription,
                    parent.Project));
                drawingNo = NullIfBlank(child.DrawingNo) ?? drawingNo;
                sequence++;
            }

            parent.DrawingNo = drawingNo;
            parent.ProductionOrderLines = mergedLines;
            var payload = PrepareProductionOrderForSap(parent);
            var created = await httpRequestHandler.PostAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                Constants.SapApiUrls.CreateProductionOrder, payload);
            if (created?.Error is not null || created?.AbsoluteEntry is not > 0)
                return created ?? parent;

            payload.AbsoluteEntry = created.AbsoluteEntry;
            payload.DocumentNumber = created.DocumentNumber ?? payload.DocumentNumber;
            // POST often returns AbsoluteEntry only. Read the live document for DocumentNumber
            // and LineNumber so the retag PUT is an in-place update, not a collection replace.
            var live = await TryGetSapProductionOrderAsync(created.AbsoluteEntry, cancellationToken);
            if (created.DocumentNumber is not > 0 && live?.DocumentNumber is > 0)
                created.DocumentNumber = live.DocumentNumber;

            payload.DocumentNumber = created.DocumentNumber ?? payload.DocumentNumber;
            var assignedDocNum = created.DocumentNumber;
            if (assignedDocNum is > 0 && payload.ProductionOrderLines is { Count: > 0 })
            {
                if (live?.ProductionOrderLines is { Count: > 0 })
                {
                    RetagDraftSubassemblyLines(live.ProductionOrderLines, assignedDocNum.Value);
                    PrepareLiveDocumentForInPlacePut(live);
                    try
                    {
                        await httpRequestHandler.PutAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                            Constants.SapApiUrls.GetProductionOrders(created.AbsoluteEntry.Value.ToString()),
                            live);
                        payload = live;
                    }
                    catch (ApiErrorException ex)
                    {
                        Serilog.Log.Warning(
                            ex,
                            "Could not retag sub-assembly lines on production order {AbsoluteEntry} from 0-n to {DocumentNumber}-n",
                            created.AbsoluteEntry,
                            assignedDocNum);
                    }
                }
            }

            await localStore.UpsertFromSapAsync(payload, cancellationToken: cancellationToken);
            await RefreshMirrorAfterWriteAsync(created.AbsoluteEntry, created, cancellationToken);

            sequence = 1;
            foreach (var child in children)
            {
                child.ParentAbsoluteEntry = created.AbsoluteEntry;
                var draftTag = $"{DraftParentDocumentNumber}/{sequence}";
                child.ParentProductionOrderNo = assignedDocNum is > 0
                    ? ProductionOrderSubassemblyTag.WithParent(draftTag, assignedDocNum.Value.ToString())
                    : draftTag;
                child.DocumentNumber = created.DocumentNumber;
                child.ItemNumber = parent.ItemNumber ?? child.ItemNumber;
                child.Warehouse = child.Warehouse ?? parent.Warehouse;
                child.Project = child.Project ?? parent.Project;
                child.ProjectName = child.ProjectName ?? parent.ProjectName;
                await localStore.InsertVirtualSubassemblyAsync(child, created, cancellationToken);
                sequence++;
            }

            return await localStore.GetFromDbAsync(created.AbsoluteEntry.Value, includeLines: true, cancellationToken)
                ?? created;
        }

        async Task<SapProductionOrdersResponse> CreateVirtualSubassemblyAsync(
            SapProductionOrdersResponse request,
            CancellationToken cancellationToken)
        {
            var parent = await localStore.FindParentOfSubassemblyAsync(
                request.ParentAbsoluteEntry,
                request.ParentProductionOrderNo,
                cancellationToken)
                ?? throw new ApiErrorException(
                    BaseErrorCodes.ValidationFailed,
                    "Parent production order was not found for this sub-assembly.");

            var created = await localStore.InsertVirtualSubassemblyAsync(request, parent, cancellationToken);
            var itemLines = (request.ProductionOrderLines ?? [])
                .Where(line => !string.IsNullOrWhiteSpace(line.ItemNo))
                .ToList();
            if (itemLines.Count == 0)
                return created;

            request.AbsoluteEntry = created.AbsoluteEntry;
            return await UpdateVirtualSubassemblyAsync(request, cancellationToken)
                ?? created;
        }

        async Task<SapProductionOrdersResponse?> UpdateVirtualSubassemblyAsync(
            SapProductionOrdersResponse request,
            CancellationToken cancellationToken)
        {
            if (request.AbsoluteEntry is null)
                throw new ApiErrorException(
                    BaseErrorCodes.ValidationFailed,
                    "Sub-assembly was not found.");

            var existing = await localStore.GetFromDbAsync(
                request.AbsoluteEntry.Value,
                includeLines: true,
                cancellationToken)
                ?? throw new ApiErrorException(
                    BaseErrorCodes.ValidationFailed,
                    "Sub-assembly was not found.");

            var storedTag = NullIfBlank(existing.ParentProductionOrderNo)
                ?? NullIfBlank(request.ParentProductionOrderNo);
            var cancelled = string.Equals(
                request.Status,
                Constants.SapProductionOrderStatus.Cancelled,
                StringComparison.OrdinalIgnoreCase);
            var itemLines = (request.ProductionOrderLines ?? [])
                .Where(line => !string.IsNullOrWhiteSpace(line.ItemNo))
                .ToList();

            if (cancelled || itemLines.Count > 0)
            {
                var parent = await localStore.FindParentOfSubassemblyAsync(
                    existing.ParentAbsoluteEntry ?? request.ParentAbsoluteEntry,
                    storedTag,
                    cancellationToken)
                    ?? throw new ApiErrorException(
                        BaseErrorCodes.ValidationFailed,
                        "Parent production order was not found for this sub-assembly.");

                var tag = ProductionOrderSubassemblyTag.WithParent(
                    storedTag,
                    parent.DocumentNumber?.ToString())
                    ?? storedTag;
                request.ParentProductionOrderNo = tag;

                var sapParent = await TryGetSapProductionOrderAsync(parent.AbsoluteEntry, cancellationToken);
                var live = sapParent ?? parent;
                var merged = MergeSubassemblyLinesOntoParent(
                    live,
                    tag,
                    cancelled ? [] : itemLines,
                    request.DrawingNo,
                    request.ProductDescription);
                merged.ProjectName = NullIfBlank(merged.ProjectName)
                    ?? NullIfBlank(parent.ProjectName)
                    ?? NullIfBlank(request.ProjectName);

                // Issued or closed rows must keep LineNumber. A full collection replace that
                // drops identity fails with Error -1 once any component has been issued.
                var replaceEntire = !HasProtectedLines(live.ProductionOrderLines);
                var updated = await PutProductionOrderToSapAsync(
                    merged,
                    cancellationToken,
                    preserveExistingLineStorage: true,
                    replaceEntireLineCollection: replaceEntire);

                if (updated?.Error is not null)
                    return updated;
            }

            var child = await localStore.UpdateVirtualHeaderAsync(request, cancellationToken)
                ?? throw new ApiErrorException(
                    BaseErrorCodes.ValidationFailed,
                    "Sub-assembly was not found.");

            return await localStore.GetFromDbAsync(child.AbsoluteEntry!.Value, includeLines: true, cancellationToken)
                ?? child;
        }

        static SapProductionOrdersResponse MergeSubassemblyLinesOntoParent(
            SapProductionOrdersResponse parent,
            string? tag,
            IReadOnlyList<SapProductionOrderLines> replacement,
            string? drawingNo = null,
            string? drawingName = null)
        {
            var parentLines = parent.ProductionOrderLines ?? [];
            var keep = parentLines
                .Where(line => !ProductionOrderSubassemblyTag.EqualsTag(line.DocNum, tag))
                .ToList();
            var existingTagged = parentLines
                .Where(line => ProductionOrderSubassemblyTag.EqualsTag(line.DocNum, tag))
                .ToList();
            var incoming = StampSubassemblyLinesForParent(
                replacement,
                parentLines,
                existingTagged,
                tag,
                drawingNo,
                drawingName,
                parent.Project);
            var unmatchedProtected = existingTagged
                .Where(existing =>
                    IsProtectedLine(existing)
                    && incoming.All(line => line.LineNumber is null || line.LineNumber != existing.LineNumber))
                .Select(existing =>
                {
                    existing.DocNum = ProductionOrderSubassemblyTag.ToSapTag(tag);
                    return existing;
                })
                .ToList();
            parent.ParentProductionOrderNo = null;
            parent.ParentAbsoluteEntry = null;
            parent.DrawingNo = NullIfBlank(drawingNo) ?? NullIfBlank(parent.DrawingNo);
            parent.ProductionOrderLines = keep.Concat(incoming).Concat(unmatchedProtected).ToList();
            return parent;
        }

        static List<SapProductionOrderLines> StampSubassemblyLinesForParent(
            IReadOnlyList<SapProductionOrderLines> replacement,
            IReadOnlyList<SapProductionOrderLines> parentLines,
            IReadOnlyList<SapProductionOrderLines>? existingTagged,
            string? tag,
            string? drawingNo = null,
            string? drawingName = null,
            string? project = null)
        {
            var sapDrawingName = NullIfBlank(drawingName);
            var sapDrawingNo = NullIfBlank(drawingNo);
            var sapProject = NullIfBlank(project);
            var unused = (existingTagged ?? []).ToList();
            return replacement
                .Select(line =>
                {
                    var match = TakeMatchingTaggedLine(unused, line);
                    line.DocNum = ProductionOrderSubassemblyTag.ToSapTag(tag);
                    if (match is not null)
                    {
                        line.LineNumber = match.LineNumber;
                        line.DocumentAbsoluteEntry = match.DocumentAbsoluteEntry;
                        if (IsProtectedLine(match))
                        {
                            line.ItemNo = match.ItemNo;
                            // Prepare omits BaseQuantity on issued/closed rows. Keep those flags
                            // until then or SAP Error -1 on a PUT of a released order.
                            line.IssuedQuantity = match.IssuedQuantity;
                            line.LineStatus = match.LineStatus;
                            if (line.PlannedQuantity < (match.IssuedQuantity ?? 0))
                                line.PlannedQuantity = match.IssuedQuantity ?? line.PlannedQuantity;
                        }
                    }
                    else
                    {
                        line.LineNumber = null;
                        line.DocumentAbsoluteEntry = null;
                    }
                    if (string.IsNullOrWhiteSpace(line.ItemType as string))
                        line.ItemType = "pit_Item";
                    if (string.IsNullOrWhiteSpace(line.ProductionOrderIssueType))
                        line.ProductionOrderIssueType = "im_Manual";
                    // WOR1 item lines reject LineText. Drawing No is WOR1 U_DwgNo; drawing name
                    // defaults U_FreeTxt when the row has no user free text (no Drawing Name UDF).
                    line.LineText = null;
                    line.DrawingNo = Truncate(NullIfBlank(line.DrawingNo) ?? sapDrawingNo, 30);
                    var freeText = NullIfBlank(line.FreeText) ?? sapDrawingName;
                    line.FreeText = freeText is null ? null : Truncate(freeText, 254);
                    if (string.IsNullOrWhiteSpace(line.Project) && sapProject is not null)
                        line.Project = sapProject;
                    if (line.LocationCode is null)
                    {
                        var matchingBin = parentLines.FirstOrDefault(existing =>
                            existing.LocationCode is not null
                            && string.Equals(
                                existing.Warehouse,
                                line.Warehouse,
                                StringComparison.OrdinalIgnoreCase));
                        line.LocationCode = matchingBin?.LocationCode;
                    }

                    return line;
                })
                .ToList();
        }

        static SapProductionOrderLines? TakeMatchingTaggedLine(
            List<SapProductionOrderLines> unused,
            SapProductionOrderLines incoming)
        {
            var byNumber = incoming.LineNumber is int lineNumber
                ? unused.Find(existing => existing.LineNumber == lineNumber)
                : null;
            var match = byNumber ?? unused.Find(existing =>
                string.Equals(existing.ItemNo, incoming.ItemNo, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(incoming.Warehouse)
                    || string.Equals(existing.Warehouse, incoming.Warehouse, StringComparison.OrdinalIgnoreCase)));
            if (match is not null)
                unused.Remove(match);
            return match;
        }

        static bool HasProtectedLines(IEnumerable<SapProductionOrderLines>? lines) =>
            lines?.Any(IsProtectedLine) == true;

        static bool IsProtectedLine(SapProductionOrderLines line) =>
            (line.IssuedQuantity ?? 0) > 0
            || string.Equals(line.LineStatus, "bost_Close", StringComparison.OrdinalIgnoreCase);

        static void RetagDraftSubassemblyLines(IEnumerable<SapProductionOrderLines> lines, int documentNumber)
        {
            var parent = documentNumber.ToString();
            foreach (var line in lines)
            {
                var retagged = ProductionOrderSubassemblyTag.WithParent(line.DocNum, parent);
                if (retagged is not null)
                    line.DocNum = ProductionOrderSubassemblyTag.ToSapTag(retagged);
            }
        }

        /// <summary>
        /// Service Layer PUT is a full replace. Keep SAP-locked identity (item, type, warehouse,
        /// posting date, origin) from the live document and apply only the fields the form may edit.
        /// </summary>
        async Task<SapProductionOrdersResponse> OverlayEditableHeaderOntoLiveSapAsync(
            SapProductionOrdersResponse incoming,
            bool headerOnly,
            CancellationToken cancellationToken)
        {
            var live = await TryGetSapProductionOrderAsync(incoming.AbsoluteEntry, cancellationToken);
            if (live is null)
                return incoming;

            live.Remarks = incoming.Remarks;
            if (incoming.DueDate is { } due && due != default)
                live.DueDate = due;
            if (incoming.StartDate is { } start && start != default)
                live.StartDate = start;
            if (incoming.PlannedQuantity > 0)
                live.PlannedQuantity = incoming.PlannedQuantity;
            if (!string.IsNullOrWhiteSpace(incoming.Status))
                live.Status = incoming.Status;
            live.DrawingNo = NullIfBlank(incoming.DrawingNo) ?? live.DrawingNo;
            live.ProjectName = NullIfBlank(incoming.ProjectName) ?? live.ProjectName;
            live.ProductDescription = NullIfBlank(incoming.ProductDescription) ?? live.ProductDescription;
            live.ProductionCategory = NullIfBlank(incoming.ProductionCategory) ?? live.ProductionCategory;
            live.AbsoluteEntry = incoming.AbsoluteEntry ?? live.AbsoluteEntry;

            if (!headerOnly)
                live.ProductionOrderLines = incoming.ProductionOrderLines;

            return live;
        }

        /// <summary>
        /// PUT of a just-created production order rejects the slim Prepare body (Error -1) but
        /// accepts the live GET minus calculated fields. LineNumber 0 must stay (omitting it is
        /// treated as a collection replace). VisualOrder 0 is omitted.
        /// </summary>
        static void PrepareLiveDocumentForInPlacePut(SapProductionOrdersResponse order)
        {
            order.AbsoluteEntry = null;
            order.DocumentNumber = null;
            order.Series = null;
            order.CompletedQuantity = null;
            order.RejectedQuantity = null;
            order.Priority = null;
            order.CreationDate = null;
            order.UserSignature = null;
            order.Printed = null;
            order.TransactionNumber = null;
            order.ClosingDate = null;
            order.ReleaseDate = null;
            order.InventoryUom = null;
            order.JournalRemarks = null;
            order.PickRemarks = null;
            order.RoutingDateCalculation = null;
            order.UpdateAllocation = null;
            order.AttachmentEntry = null;
            order.SapPassport = null;
            order.CustomerName = null;
            order.ParentProductionOrderNo = null;
            order.ParentAbsoluteEntry = null;
            order.Weight = null;
            order.Subassemblies = null;
            order.ProductionOrdersSalesOrderLines = null;
            order.ProductionOrdersStages = null;
            order.ProductionOrdersDocumentReferences = null;
            order.ODataMetadata = null;
            order.ODataNextLink = null;
            order.Error = null;

            foreach (var line in order.ProductionOrderLines ?? [])
            {
                line.IssuedQuantity = null;
                line.LineStatus = null;
                line.ItemName = null;
                line.SerialNumbers = null;
                line.BatchNumbers = null;
                line.StartDate = null;
                line.EndDate = null;
                line.RequiredDays = null;
                line.AdditionalQuantity = null;
                line.WeightOfRecycledPlastic = null;
                line.UoMCode = null;
                line.UoMEntry = null;
                line.VisualOrder = null;
                line.LineText = null;
                line.BaseQuantity ??= line.PlannedQuantity;
            }
        }

        async Task<SapProductionOrdersResponse> PreserveTaggedSubassemblyLinesAsync(
            SapProductionOrdersResponse payload,
            CancellationToken cancellationToken)
        {
            if (payload.AbsoluteEntry is null or <= 0)
                return payload;

            // Header-only updates omit lines. Service Layer PUT treats a missing collection as
            // "no components" (Error: Must have components). Keep the live SAP rows instead.
            if (payload.ProductionOrderLines is not { Count: > 0 })
            {
                var sap = await TryGetSapProductionOrderAsync(payload.AbsoluteEntry, cancellationToken);
                var keep = sap?.ProductionOrderLines
                    ?? (await localStore.GetFromDbAsync(
                        payload.AbsoluteEntry.Value,
                        includeLines: true,
                        cancellationToken))?.ProductionOrderLines;
                if (keep is { Count: > 0 })
                    payload.ProductionOrderLines = keep;
                return payload;
            }

            var current = await localStore.GetFromDbAsync(
                payload.AbsoluteEntry.Value,
                includeLines: true,
                cancellationToken);
            var tagged = (current?.ProductionOrderLines ?? [])
                .Where(line => ProductionOrderSubassemblyTag.IsTagged(line.DocNum))
                .Select(line =>
                {
                    line.DocNum = ProductionOrderSubassemblyTag.ToSapTag(line.DocNum);
                    return line;
                })
                .ToList();
            if (tagged.Count == 0)
                return payload;

            var untagged = payload.ProductionOrderLines
                .Where(line => !ProductionOrderSubassemblyTag.IsTagged(line.DocNum))
                .ToList();
            payload.ProductionOrderLines = untagged.Concat(tagged).ToList();
            return payload;
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
        static SapProductionOrdersResponse PrepareProductionOrderForSap(
            SapProductionOrdersResponse order,
            IReadOnlySet<int>? existingLineNumbers = null,
            bool preserveExistingLineStorage = false,
            bool replaceEntireLineCollection = false)
        {
            var parentNo = NullIfBlank(order.ParentProductionOrderNo);
            // Service Layer metadata for ProductionOrder does not expose OWOR U_DocNum (name
            // collision with DocumentNumber). Sending it fails the whole write. WOR1 U_DocNum
            // ("Subassembly") is exposed on lines — stamp the parent/sequence there instead.
            order.ParentProductionOrderNo = null;
            order.ParentAbsoluteEntry = null;
            order.Weight = null;
            order.DrawingNo = NullIfBlank(order.DrawingNo);
            order.ProductionCategory = NullIfBlank(order.ProductionCategory);
            order.UoMEntry = SapProductionOrderUoMNormalizer.NormalizeUoMEntry(order.UoMEntry);
            // Calculated / read-only on OWOR. Sending them on a released issued order is Error -1.
            order.CompletedQuantity = null;
            order.RejectedQuantity = null;
            order.Priority = null;
            order.CreationDate = null;
            order.UserSignature = null;
            order.Printed = null;
            order.TransactionNumber = null;
            order.ClosingDate = null;
            order.ReleaseDate = null;
            order.InventoryUom = null;
            order.JournalRemarks = null;
            order.PickRemarks = null;
            order.RoutingDateCalculation = null;
            order.UpdateAllocation = null;
            order.AttachmentEntry = null;
            order.SapPassport = null;
            if (order.AbsoluteEntry is > 0)
                order.Series = null;
            // PostingDate must stay: PUT treats an omitted PostDate as a change, which Live
            // rejects with ODBC -1029. Overlay copies the live value; the form cannot edit it.

            var receiptWarehouse = NullIfBlank(order.Warehouse);
            var issuingWarehouse = preserveExistingLineStorage
                ? null
                : order.ProductionOrderLines?
                    .Select(line => NullIfBlank(line.Warehouse))
                    .FirstOrDefault(warehouse =>
                        warehouse is not null
                        && !string.Equals(warehouse, receiptWarehouse, StringComparison.OrdinalIgnoreCase));

            var lines = order.ProductionOrderLines?
                .Where(line => !string.IsNullOrWhiteSpace(line.ItemNo))
                .Select((line, index) =>
                {
                    line.VisualOrder = index;
                    line.SerialNumbers = null;
                    line.BatchNumbers = null;
                    // ProductionOrderLine.UoMCode must be a positive UoM entry. Drop inventory
                    // names ("KG") and Manual-group placeholder -1 (SAP Error -1 on commit).
                    line.UoMCode = SapProductionOrderUoMNormalizer.NormalizeUoMCode(line.UoMCode);
                    line.UoMEntry = SapProductionOrderUoMNormalizer.NormalizeUoMEntry(line.UoMEntry);
                    // LineText is only valid on text-type rows. Sending it on item lines fails PUT.
                    line.LineText = null;
                    // These are read/calculated on WOR1. A full collection replace that sends them
                    // fails with Error -1 even when LineNumber is omitted.
                    line.IssuedQuantity = null;
                    line.LineStatus = null;
                    line.ItemName = null;
                    // SAP KBA 3532907: empty Base Qty on a planned update is reported as
                    // "Planned quantity is missing". Create still omits it so SAP fills it.
                    if (order.AbsoluteEntry is > 0)
                        line.BaseQuantity ??= line.PlannedQuantity;
                    else
                        line.BaseQuantity = null;
                    line.AdditionalQuantity = null;
                    line.WeightOfRecycledPlastic = null;
                    line.StartDate = null;
                    line.EndDate = null;
                    line.RequiredDays = null;
                    if (string.IsNullOrWhiteSpace(line.ProductionOrderIssueType))
                        line.ProductionOrderIssueType = "im_Manual";
                    if (parentNo is not null && string.IsNullOrWhiteSpace(line.DocNum))
                        line.DocNum = parentNo;
                    if (replaceEntireLineCollection)
                    {
                        // Full collection replace. Mixing LineNumber on kept BOM rows with omitted
                        // identity on new tagged rows is SAP Error -1.
                        line.LineNumber = null;
                        line.DocumentAbsoluteEntry = null;
                    }
                    else
                    {
                        var isExistingLine = existingLineNumbers is { Count: > 0 }
                            && line.LineNumber is int lineNumber
                            && existingLineNumbers.Contains(lineNumber);
                        if (isExistingLine)
                        {
                            line.DocumentAbsoluteEntry = order.AbsoluteEntry;
                        }
                        else if (existingLineNumbers is { Count: > 0 })
                        {
                            // LineNumber 1 when SAP only has line 0 is treated as an in-place update of
                            // a row that does not exist. Omit identity so Service Layer appends.
                            line.LineNumber = null;
                            line.DocumentAbsoluteEntry = null;
                        }
                        else
                        {
                            line.DocumentAbsoluteEntry = order.AbsoluteEntry;
                        }
                    }
                    // Bin locations are warehouse-specific. A Store1 bin on a line whose warehouse
                    // was rewritten (or on a newly added WIP row) fails the whole PUT with Error -1.
                    // Virtual sub-assembly writes keep parent BOM bins and copy them onto new lines.
                    if (!preserveExistingLineStorage)
                        line.LocationCode = null;
                    if (issuingWarehouse is not null
                        && string.Equals(line.Warehouse, receiptWarehouse, StringComparison.OrdinalIgnoreCase))
                    {
                        line.Warehouse = issuingWarehouse;
                    }
                    return line;
                })
                .ToList();
            // Empty [] blocks BOM explosion and SAP rejects "Must have components". Omit the
            // collection when there are no item rows so a create can still succeed, or an update
            // leaves existing lines alone.
            order.ProductionOrderLines = lines is { Count: > 0 } ? lines : null;

            // U_CustomerName does not exist on OWOR. U_PrjName does — keep ProjectName so the
            // header project name reaches SAP.
            order.CustomerName = null;

            order.ProductionOrdersSalesOrderLines = null;
            order.ProductionOrdersStages = null;
            order.ProductionOrdersDocumentReferences = null;
            order.Subassemblies = null;
            order.ODataMetadata = null;
            order.ODataNextLink = null;
            order.Error = null;

            return order;
        }

        async Task<HashSet<int>?> ExistingLineNumbersAsync(int? absoluteEntry, CancellationToken cancellationToken)
        {
            if (absoluteEntry is null or <= 0)
                return null;

            var current = await localStore.GetFromDbAsync(absoluteEntry.Value, includeLines: true, cancellationToken);
            var numbers = current?.ProductionOrderLines?
                .Where(line => line.LineNumber is not null)
                .Select(line => line.LineNumber!.Value)
                .ToHashSet();
            return numbers is { Count: > 0 } ? numbers : null;
        }

        async Task<SapProductionOrdersResponse?> TryGetSapProductionOrderAsync(
            int? absoluteEntry,
            CancellationToken cancellationToken)
        {
            if (absoluteEntry is not > 0)
                return null;

            try
            {
                var sap = await httpRequestHandler.GetOrThrowAsync<SapProductionOrdersResponse>(
                    Constants.SapApiUrls.GetProductionOrders(absoluteEntry.Value.ToString()),
                    cancellationToken);
                return sap?.AbsoluteEntry is > 0 ? sap : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static string? Truncate(string? value, int maxLength) =>
            value is null ? null : value.Length <= maxLength ? value : value[..maxLength];

        static string? NullIfBlank(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        static SapProductionOrderLines CopyItemLine(SapProductionOrderLines line) => new()
        {
            ItemNo = line.ItemNo,
            ItemName = line.ItemName,
            PlannedQuantity = line.PlannedQuantity,
            Warehouse = line.Warehouse,
            ProductionOrderIssueType = line.ProductionOrderIssueType,
            DrawingNo = line.DrawingNo,
            FreeText = line.FreeText,
            Project = line.Project,
            LocationCode = line.LocationCode,
        };

        static SapProductionOrderLines PrepareLineForSapPatch(
            SapProductionOrderLines line,
            bool preserveLocationCode = false)
        {
            // Identity fields must stay unset: a LineNumber that already exists is treated as an
            // in-place update, which cannot change ItemNo.
            line.LineNumber = null;
            line.VisualOrder = null;
            line.DocumentAbsoluteEntry = null;
            line.SerialNumbers = null;
            line.BatchNumbers = null;
            if (!preserveLocationCode)
                line.LocationCode = null;
            line.UoMCode = SapProductionOrderUoMNormalizer.NormalizeUoMCode(line.UoMCode);
            line.UoMEntry = SapProductionOrderUoMNormalizer.NormalizeUoMEntry(line.UoMEntry);
            return line;
        }
    }
}
