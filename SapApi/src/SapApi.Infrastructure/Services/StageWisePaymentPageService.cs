using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Responses;
using SapApi.Shared.Responses.Sap;
using Serilog;

namespace SapApi.Infrastructure.Services;

public class StageWisePaymentPageService(
    AppDbContext db,
    SapPurchaseOrderService purchaseOrderService,
    SapVendorPaymentService vendorPaymentService,
    SapMasterDataService masterDataService,
    PurchaseOrderLinkResolver purchaseOrderLinks,
    ISapLoginService sapLogin,
    ICurrentCompanyDbAccessor companyDbAccessor)
{
    private string CompanyDb => companyDbAccessor.GetCompanyDbName();

    public async Task<StageWisePaymentPageDataResponse?> LoadPageDataAsync(int poDocEntry, CancellationToken cancellationToken = default)
    {
        await sapLogin.SapLoginAsync(cancellationToken);

        var po = await purchaseOrderService.GetPurchaseOrderForPaymentPage(poDocEntry.ToString(), cancellationToken);
        if (po?.Error?.Message?.Value is { } sapError)
            throw new ApiErrorException("SYS-01", sapError);
        if (po is null || po.DocEntry is null)
            return null;

        var docNum = po.DocNum ?? poDocEntry;
        var purchaseOrderId = await purchaseOrderLinks.EnsureIdFromSapPoAsync(po, cancellationToken);
        var tableRecords = await db.StageWisePayments
            .AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb
                && (purchaseOrderId != null
                    ? x.PurchaseOrderId == purchaseOrderId
                    : x.DocNumber == docNum))
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var activeRecords = tableRecords.Where(x => x.Status != StageWisePaymentStatus.Cancelled).ToList();
        var paymentTerms = po.CreateUdfList();
        var batchTermMap = await LoadBatchTermMapAsync(
            tableRecords.Where(StageWisePaymentCalculations.IsBatchPaymentRecord).ToList(),
            cancellationToken);
        var calcActiveRecords = StageWisePaymentCalculations.ExpandActiveRecordsForTermCalculations(
            activeRecords, batchTermMap, paymentTerms);
        var totalBasic = (po.DocTotal ?? 0) - (po.VatSum ?? 0);

        var projectNameTask = masterDataService.GetProjectNameAsync(po.Project, cancellationToken);
        var apInvoicesTask = LoadApInvoicesAsync(po, cancellationToken);
        var wtCodesTask = LoadWithholdingTaxCodesAsync(po.CardCode, cancellationToken);
        var approvalsTask = LoadLinkedApprovalsAsync(tableRecords, cancellationToken);

        await Task.WhenAll(projectNameTask, apInvoicesTask, wtCodesTask, approvalsTask);

        var linkedApprovals = await approvalsTask;
        var banks = Constants.BankAccounts.GetBanksForBplId(po.BPLId)
            .Select(b => new StageWisePaymentBankOption { Key = b.Key, Value = b.Value })
            .ToList();

        return new StageWisePaymentPageDataResponse
        {
            PurchaseOrder = po,
            ProjectName = await projectNameTask,
            TotalBasic = totalBasic,
            BalancePayment = StageWisePaymentCalculations.GetBalancePayment(po, activeRecords),
            PaymentTerms = paymentTerms,
            TableRecords = tableRecords.Select(r => MapRecord(r, batchTermMap, linkedApprovals)).ToList(),
            // Expanded so FE stage payable subtracts prior batch Gross/Gst by payment term.
            ActiveRecords = calcActiveRecords.Select(r => MapRecord(r, batchTermMap, linkedApprovals)).ToList(),
            Banks = banks,
            BankLabels = Constants.BankAccounts.Banks,
            ApInvoices = await apInvoicesTask,
            WithholdingTaxCodes = await wtCodesTask,
            PaymentSummary = StageWisePaymentCalculations.BuildPaymentSummary(po, activeRecords),
        };
    }

    private async Task<IReadOnlyDictionary<int, ApprovalRequest>> LoadLinkedApprovalsAsync(
        IReadOnlyList<StageWisePayment> tableRecords,
        CancellationToken cancellationToken)
    {
        var approvalIds = tableRecords
            .SelectMany(r => ParseApprovalRequestIds(r.ApprovalRequestId))
            .Distinct()
            .ToList();
        if (approvalIds.Count == 0)
            return new Dictionary<int, ApprovalRequest>();

        var rows = await db.ApprovalRequests
            .AsNoTracking()
            .Where(r => r.CompanyDb == CompanyDb && approvalIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Id);
    }

    private async Task<Dictionary<int, IReadOnlyList<int>>> LoadBatchTermMapAsync(
        IReadOnlyList<StageWisePayment> batchRecords,
        CancellationToken cancellationToken)
    {
        var batchPaymentIds = batchRecords
            .Where(x => x.PaymentTermsType is null)
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        if (batchPaymentIds.Count == 0)
            return new Dictionary<int, IReadOnlyList<int>>();

        var batches = await db.StageWisePaymentBatches
            .AsNoTracking()
            .Include(b => b.Lines).ThenInclude(l => l.PaymentTerms)
            .Where(b => b.CompanyDb == CompanyDb
                && ((b.StageWisePaymentId != null && batchPaymentIds.Contains(b.StageWisePaymentId.Value))
                    || (b.DownPaymentStageWisePaymentId != null
                        && batchPaymentIds.Contains(b.DownPaymentStageWisePaymentId.Value))))
            .ToListAsync(cancellationToken);

        var map = new Dictionary<int, IReadOnlyList<int>>();
        foreach (var batch in batches)
        {
            var termIds = batch.Lines
                .SelectMany(l => l.PaymentTerms)
                .Select(t => t.PaymentTermsType)
                .Distinct()
                .ToList();

            if (batch.StageWisePaymentId is int primaryId && batchPaymentIds.Contains(primaryId))
                map[primaryId] = termIds;
            if (batch.DownPaymentStageWisePaymentId is int dpId
                && batchPaymentIds.Contains(dpId)
                && (!map.ContainsKey(dpId) || termIds.Count > 0))
            {
                map[dpId] = termIds;
            }
        }

        return map;
    }

    public async Task<SapPurchaseInvoicesResponse?> ResolveApInvoiceAsync(
        SapPurchaseOrdersResponse po,
        string? apInvoiceDocEntry,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(apInvoiceDocEntry))
            return null;

        var apInvoices = await LoadApInvoicesAsync(po, cancellationToken);
        return apInvoices.FirstOrDefault(x => x.DocEntry.ToString() == apInvoiceDocEntry);
    }

    private async Task<List<SapPurchaseInvoicesResponse>> LoadApInvoicesAsync(
        SapPurchaseOrdersResponse po,
        CancellationToken cancellationToken)
    {
        if (po.DocEntry is null)
            return [];

        var cardCode = po.CardCode ?? string.Empty;
        var poDocEntry = po.DocEntry.Value;

        // A purchase order can be invoiced directly and through goods receipts at the same time,
        // so the receipts are resolved first and both link types are collected in one invoice scan.
        var grpos = await vendorPaymentService.GetGrposForPurchaseOrder(cardCode, poDocEntry);
        if (grpos is null || grpos.Error is not null)
        {
            Log.Warning(
                "Could not list goods receipt POs for purchase order {PoDocEntry} ({CompanyDb}); "
                + "AP invoices raised through a receipt may be missing from the picker. SAP said: {SapMessage}",
                poDocEntry,
                CompanyDb,
                grpos?.Error?.Message?.Value ?? "no response");
        }

        var grpoDocEntries = grpos?.Value?
            .Where(x => x.DocEntry.HasValue)
            .Select(x => x.DocEntry!.Value)
            .Distinct()
            .ToList() ?? [];

        var invoices = await vendorPaymentService.GetApInvoicesForPurchaseOrder(cardCode, poDocEntry, grpoDocEntries);
        if (invoices is null || invoices.Error is not null)
        {
            Log.Warning(
                "Could not list AP invoices for purchase order {PoDocEntry} ({CompanyDb}); the payment page "
                + "will show no selectable invoice. SAP said: {SapMessage}",
                poDocEntry,
                CompanyDb,
                invoices?.Error?.Message?.Value ?? "no response");
            return [];
        }

        return invoices.Value ?? [];
    }

    private async Task<List<StageWisePaymentWtCodeOption>> LoadWithholdingTaxCodesAsync(
        string? cardCode,
        CancellationToken cancellationToken)
    {
        var partner = await masterDataService.GetBusinessPartnerByCardCodeAsync(cardCode ?? string.Empty, cancellationToken: cancellationToken);
        var wtCodes = partner?.WithholdingTaxDataCollectionResponse?
            .Select(wt => wt.WtCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (wtCodes.Count == 0)
            return [];

        var wtMaster = await masterDataService.GetWithholdingTaxByCodesAsync(wtCodes, cancellationToken);
        return wtCodes.Select(code => new StageWisePaymentWtCodeOption
        {
            WtCode = code,
            WtName = wtMaster.FirstOrDefault(m => m.WtCode == code)?.WtName,
            Rate = wtMaster.FirstOrDefault(m => m.WtCode == code)?.Rate,
        }).ToList();
    }

    private static StageWisePaymentRecordDto MapRecord(
        StageWisePayment record,
        IReadOnlyDictionary<int, IReadOnlyList<int>>? batchTermMap = null,
        IReadOnlyDictionary<int, ApprovalRequest>? linkedApprovals = null)
    {
        linkedApprovals ??= new Dictionary<int, ApprovalRequest>();
        var (canRetrySap, retryRequestId) = ResolveRetrySap(record, linkedApprovals);

        return new StageWisePaymentRecordDto
        {
            Id = record.Id,
            PaymentTermsType = record.PaymentTermsType,
            PaymentRequestId = StageWisePaymentService.FormatPaymentRequestId(record.Id),
            StageDesc = record.StageDesc,
            Bank = record.Bank,
            UtrNo = record.UtrNo,
            UtrDate = record.UtrDate,
            ApprovalRequestId = record.ApprovalRequestId,
            ApInvoiceDocEntry = record.ApInvoiceDocEntry,
            ApDownPaymentInvoiceEntryNumber = record.ApDownPaymentInvoiceEntryNumber,
            OutgoingPaymentNumber = ResolveOutgoingPaymentNumber(record),
            WtCode = record.WtCode,
            GrossAmount = record.GrossAmount,
            GstAmount = record.GstAmount,
            Tds = record.Tds,
            Status = MapStatus(record, linkedApprovals),
            DocNumber = record.DocNumber,
            CreatedOn = record.CreatedOn,
            LastModifiedOn = record.LastModifiedOn,
            CanRetrySap = canRetrySap,
            RetrySapApprovalRequestId = retryRequestId,
        };
    }

    private static string? ResolveOutgoingPaymentNumber(StageWisePayment record)
    {
        var documentNumbers = record.ApDownPaymentInvoiceEntryNumber?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (documentNumbers is not { Length: > 0 })
            return null;

        // Outgoing payment DocNum is appended last when PaymentDocEntry is set.
        // Batch AP payments store only the outgoing payment number.
        if (!string.IsNullOrWhiteSpace(record.PaymentDocEntry)
            || record.StageDesc == "Batch AP payment")
        {
            return documentNumbers[^1];
        }

        return null;
    }

    /// <summary>
    /// Row-level Retry when workflow approvals are done but SAP posting is missing/failed.
    /// Reuses <see cref="ApprovalService.IsEligibleForSapRetry"/> (same gate as Approval Status Report).
    /// </summary>
    public static (bool CanRetry, int? ApprovalRequestId) ResolveRetrySap(
        StageWisePayment record,
        IReadOnlyDictionary<int, ApprovalRequest> linkedApprovals)
    {
        foreach (var id in ParseApprovalRequestIds(record.ApprovalRequestId))
        {
            if (!linkedApprovals.TryGetValue(id, out var approval))
                continue;
            if (!ApprovalService.IsEligibleForSapRetry(approval))
                continue;

            var sapDocsMissing = IsSapDocumentMissingForApproval(record, approval);
            var paymentAwaitingPost =
                record.Status == StageWisePaymentStatus.PendingApproval
                || approval.OverallStatus == ApprovalStatus.Failed
                || sapDocsMissing;

            if (paymentAwaitingPost)
                return (true, id);
        }

        return (false, null);
    }

    public static string MapStatus(
        StageWisePayment record,
        IReadOnlyDictionary<int, ApprovalRequest> linkedApprovals)
    {
        if (record.Status == StageWisePaymentStatus.PendingApproval
            && IsWorkflowCompleteAwaitingSap(record, linkedApprovals))
        {
            return "SAP Posting Pending";
        }

        return record.Status switch
        {
            StageWisePaymentStatus.PendingApproval => "Approval Pending",
            StageWisePaymentStatus.Approved => "Approved",
            StageWisePaymentStatus.Added => "Created",
            StageWisePaymentStatus.Cancelled => "Cancelled",
            _ => record.Status.ToString(),
        };
    }

    static bool IsWorkflowCompleteAwaitingSap(
        StageWisePayment record,
        IReadOnlyDictionary<int, ApprovalRequest> linkedApprovals)
    {
        var ids = ParseApprovalRequestIds(record.ApprovalRequestId);
        if (ids.Count == 0)
            return false;

        var linked = new List<ApprovalRequest>(ids.Count);
        foreach (var id in ids)
        {
            if (!linkedApprovals.TryGetValue(id, out var approval))
                return false;
            linked.Add(approval);
        }

        if (!linked.All(a => a.OverallStatus is ApprovalStatus.Approved or ApprovalStatus.Failed))
            return false;

        return linked.Any(a => IsSapDocumentMissingForApproval(record, a)
            || string.IsNullOrWhiteSpace(a.SapResponseDocEntry));
    }

    static bool IsSapDocumentMissingForApproval(StageWisePayment record, ApprovalRequest approval)
    {
        if (!string.IsNullOrWhiteSpace(approval.SapResponseDocEntry))
            return false;

        return approval.DocumentType switch
        {
            ApprovalDocumentType.Payments => string.IsNullOrWhiteSpace(record.PaymentDocEntry),
            ApprovalDocumentType.StagewisePayments_DP =>
                string.IsNullOrWhiteSpace(record.DownPaymentDocEntry)
                && string.IsNullOrWhiteSpace(record.ApDownPaymentInvoiceEntryNumber),
            _ => true,
        };
    }

    static List<int> ParseApprovalRequestIds(string? approvalRequestIds) =>
        approvalRequestIds?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => int.TryParse(id, out var parsed) ? parsed : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList() ?? [];
}
