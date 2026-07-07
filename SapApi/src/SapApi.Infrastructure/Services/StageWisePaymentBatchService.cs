using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Identity;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services;

public class StageWisePaymentBatchService(
    AppDbContext db,
    StageWisePaymentPageService pageService,
    StageWisePaymentService stageWisePaymentService,
    SapVendorPaymentService vendorPaymentService,
    IHttpContextAccessor httpContextAccessor,
    ICurrentCompanyDbAccessor companyDbAccessor)
{
    private string CompanyDb => companyDbAccessor.GetCompanyDbName();

    public async Task<CalculateBatchLineResponse?> CalculateLineAsync(
        CalculateBatchLineRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageData = await pageService.LoadPageDataAsync(request.PoDocEntry, cancellationToken);
        if (pageData?.PurchaseOrder is null)
            return null;

        var po = pageData.PurchaseOrder;
        var activeRecords = await GetActiveRecordsAsync(pageData.PurchaseOrder.DocNum ?? request.PoDocEntry, cancellationToken);
        var apInvoice = pageData.ApInvoices.FirstOrDefault(x => x.DocEntry.ToString() == request.ApInvoiceDocEntry);
        var totalBasic = pageData.TotalBasic;

        var (balanceDue, payable) = StageWisePaymentCalculations.ResolveBatchRowAmounts(
            po,
            pageData.PaymentTerms,
            activeRecords,
            request.PaymentTermsTypes,
            apInvoice,
            request.ApInvoiceDocEntry,
            totalBasic);

        return new CalculateBatchLineResponse
        {
            BalanceDue = balanceDue,
            Payable = payable,
        };
    }

    public async Task<(bool Success, string Message, StageWisePaymentBatchResponse? Data)> CreateBatchAsync(
        CreateStageWisePaymentBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Lines.Count == 0)
            return (false, "Add at least one payment row.", null);

        var pageData = await pageService.LoadPageDataAsync(request.PoDocEntry, cancellationToken);
        if (pageData?.PurchaseOrder is null)
            return (false, "Purchase order not found.", null);

        var po = pageData.PurchaseOrder;
        var banks = request.Lines.Select(l => l.Bank).Distinct().ToList();
        if (banks.Count != 1 || string.IsNullOrWhiteSpace(banks[0]))
            return (false, "All rows must use the same bank account.", null);

        if (string.IsNullOrWhiteSpace(request.Account))
            return (false, "Please select an account.", null);

        if (request.PostingDate is null || request.PaymentDate is null)
            return (false, "Posting date and payment date are required.", null);

        var activeRecords = await GetActiveRecordsAsync(request.DocNumber ?? po.DocNum ?? request.PoDocEntry, cancellationToken);
        var apInvoicesByDocEntry = pageData.ApInvoices
            .Where(x => x.DocEntry.HasValue)
            .ToDictionary(x => x.DocEntry!.Value.ToString(), x => x, StringComparer.Ordinal);

        var (isValid, validationMessage) = StageWisePaymentCalculations.ValidateBatchLineAmounts(
            po,
            pageData.PaymentTerms,
            activeRecords,
            request.Lines,
            pageData.TotalBasic,
            apInvoicesByDocEntry);

        if (!isValid)
            return (false, validationMessage, null);

        var (compositionValid, compositionMessage) = StageWisePaymentCalculations.ValidateBatchComposition(
            po, pageData.PaymentTerms, request.Lines);
        if (!compositionValid)
            return (false, compositionMessage, null);

        var lineSnapshots = new List<BatchLineSnapshot>();
        var paymentInvoices = new List<PaymentInvoice>();
        var totalTransfer = 0.0;
        var lineNumber = 0;
        var tdsAppliedInBatch = new HashSet<string>(StringComparer.Ordinal);

        var priorLines = new List<StageWisePaymentBatchLineRequest>();

        foreach (var (line, index) in request.Lines.Select((line, index) => (line, index)))
        {
            var requiresAp = StageWisePaymentCalculations.BatchRowRequiresApInvoice(
                po, pageData.PaymentTerms, line.PaymentTermsTypes);

            if (requiresAp)
            {
                var apInvoice = pageData.ApInvoices.First(x => x.DocEntry.ToString() == line.ApInvoiceDocEntry);
                var (balanceDue, payable) = StageWisePaymentCalculations.ResolveBatchRowAmounts(
                    po, pageData.PaymentTerms, activeRecords, line.PaymentTermsTypes, apInvoice,
                    line.ApInvoiceDocEntry, pageData.TotalBasic);

                var priorInBatch = request.Lines
                    .Take(index)
                    .Where(l => l.ApInvoiceDocEntry == line.ApInvoiceDocEntry)
                    .Sum(l => l.Amount);
                var adjustedBalanceDue = Math.Round(Math.Max(0, balanceDue - priorInBatch), 2);
                var adjustedPayable = Math.Round(Math.Max(0, payable - priorInBatch), 2);

                var hadTdsDeducted = activeRecords.Any(x =>
                    x.ApInvoiceDocEntry == line.ApInvoiceDocEntry && (x.Tds ?? 0) != 0)
                    || !tdsAppliedInBatch.Add(line.ApInvoiceDocEntry!);
                var net = line.Amount - (hadTdsDeducted ? 0 : apInvoice.WTAmount ?? 0);
                if (net <= 0)
                    return (false, $"Net payment must be greater than zero for AP invoice {apInvoice.DocNum}.", null);

                totalTransfer += net;
                paymentInvoices.Add(new PaymentInvoice
                {
                    LineNumber = lineNumber++,
                    DocEntry = apInvoice.DocEntry,
                    InvoiceType = Constants.SapVendorPaymentInvoiceType.Invoice,
                    SumApplied = net,
                    AppliedFC = 0,
                });

                lineSnapshots.Add(new BatchLineSnapshot(
                    line, index, true, adjustedBalanceDue, adjustedPayable, line.ApInvoiceDocEntry));
            }
            else
            {
                var adjustedPayable = StageWisePaymentCalculations.ComputeSequentialStageRowPayable(
                    line, priorLines, po, pageData.PaymentTerms, activeRecords, pageData.TotalBasic);

                lineSnapshots.Add(new BatchLineSnapshot(line, index, false, 0, adjustedPayable, null));
            }

            priorLines.Add(line);
        }

        var batch = new StageWisePaymentBatch
        {
            CompanyDb = CompanyDb,
            PoDocEntry = request.PoDocEntry,
            DocNumber = request.DocNumber ?? po.DocNum,
            WtCode = request.WtCode,
            ModeOfPayment = request.ModeOfPayment ?? Constants.SapPaymentMeansType.BankTransfer,
            Account = request.Account,
            JournalRemark = request.JournalRemark,
            ReferenceNo = request.ReferenceNo,
            PostingDate = request.PostingDate,
            PaymentDate = request.PaymentDate,
            Status = StageWisePaymentBatchStatus.Draft,
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = DateTime.UtcNow,
        };

        StageWisePayment? linkedStagePayment = null;
        int? downPaymentStageWisePaymentId = null;
        string? batchApprovalId = null;

        if (paymentInvoices.Count > 0)
        {
            var vendorRequest = new SapVendorPaymentRequests
            {
                CardCode = po.CardCode ?? string.Empty,
                TransferSum = totalTransfer.ToString("F2"),
                ProjectCode = po.Project,
                PoNumber = po.DocNum?.ToString(),
                BPLId = po.BPLId ?? 1,
                PaymentInvoices = paymentInvoices,
            };
            ApplyAdditionalDetailsToVendorPayment(vendorRequest, request, banks[0]!, totalTransfer);

            var sapResponse = await vendorPaymentService.CreateVendorPayments(
                vendorRequest, supportingData: po.DocEntry?.ToString());

            if (sapResponse?.Error?.Message?.Value is not null)
                return (false, $"SAP Error: {sapResponse.Error.Message.Value}", null);

            var apLineSnapshots = lineSnapshots.Where(s => s.RequiresAp).ToList();
            var totalGross = 0.0;
            var totalGst = 0.0;
            foreach (var snapshot in apLineSnapshots)
            {
                var (gross, gst) = StageWisePaymentCalculations.SplitBatchLineAmount(
                    po,
                    pageData.PaymentTerms,
                    snapshot.Line.PaymentTermsTypes,
                    snapshot.Line.Amount,
                    pageData.TotalBasic,
                    activeRecords);
                totalGross += gross;
                totalGst += gst;
            }

            var tdsAppliedForTotal = new HashSet<string>(StringComparer.Ordinal);
            var totalTds = 0.0;
            foreach (var snapshot in apLineSnapshots)
            {
                if (string.IsNullOrWhiteSpace(snapshot.ApInvoiceDocEntry))
                    continue;
                if (!apInvoicesByDocEntry.TryGetValue(snapshot.ApInvoiceDocEntry, out var apInvoice))
                    continue;

                totalTds += StageWisePaymentCalculations.ComputeApInvoiceTdsAmount(
                    apInvoice, activeRecords, snapshot.ApInvoiceDocEntry, tdsAppliedForTotal);
            }

            linkedStagePayment = new StageWisePayment
            {
                CompanyDb = CompanyDb,
                DocNumber = batch.DocNumber,
                Bank = banks[0],
                WtCode = request.WtCode,
                GrossAmount = Math.Round(totalGross, 2),
                GstAmount = Math.Round(totalGst, 2),
                Tds = Math.Round(totalTds, 2),
                StageDesc = "Batch AP payment",
                Stage = StageWisePaymentStages.AfterReceiptOfMaterial,
                ApInvoiceDocEntry = string.Join(',', apLineSnapshots.Select(s => s.ApInvoiceDocEntry)),
                CreatedOn = DateTime.UtcNow,
                LastModifiedOn = DateTime.UtcNow,
            };

            if (sapResponse?.PendingApproval == true)
            {
                linkedStagePayment.ApprovalRequestId = sapResponse.PendingApprovalRequestId?.ToString();
                linkedStagePayment.Status = StageWisePaymentStatus.PendingApproval;
                batch.Status = StageWisePaymentBatchStatus.PendingApproval;
                batchApprovalId = linkedStagePayment.ApprovalRequestId;
            }
            else if (sapResponse?.DocEntry.HasValue == true)
            {
                linkedStagePayment.ApDownPaymentInvoiceEntryNumber = sapResponse.DocNumber?.ToString();
                linkedStagePayment.PaymentDocEntry = sapResponse.DocEntry?.ToString();
                linkedStagePayment.Status = StageWisePaymentStatus.Added;
                batch.Status = StageWisePaymentBatchStatus.Approved;
            }
            else
            {
                return (false, "No vendor payment was created in SAP.", null);
            }

            await db.StageWisePayments.AddAsync(linkedStagePayment, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var downPaymentSnapshots = lineSnapshots.Where(s => !s.RequiresAp).ToList();
        if (downPaymentSnapshots.Count > 0)
        {
            foreach (var snapshot in downPaymentSnapshots)
            {
                var term = StageWisePaymentCalculations.ResolveDownPaymentTerm(
                    pageData.PaymentTerms, snapshot.Line.PaymentTermsTypes);
                if (term is null)
                    return (false, "Payment term not found for down payment row.", null);
            }

            var downPaymentLines = downPaymentSnapshots.Select(s => s.Line).ToList();
            var (success, message, paymentId) = await stageWisePaymentService.CreateBatchDownPaymentAsync(
                po,
                downPaymentLines,
                pageData.PaymentTerms,
                pageData.TotalBasic,
                banks[0],
                request.WtCode,
                activeRecords);

            if (!success)
                return (false, message, null);

            activeRecords = await GetActiveRecordsAsync(
                request.DocNumber ?? po.DocNum ?? request.PoDocEntry, cancellationToken);

            var createdPayment = paymentId.HasValue
                ? await db.StageWisePayments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == paymentId.Value && x.CompanyDb == CompanyDb, cancellationToken)
                : null;

            if (linkedStagePayment is null && createdPayment is not null)
                linkedStagePayment = createdPayment;

            if (paymentId.HasValue)
                downPaymentStageWisePaymentId = paymentId.Value;

            if (createdPayment?.Status == StageWisePaymentStatus.PendingApproval)
            {
                batchApprovalId ??= createdPayment.ApprovalRequestId;
                batch.Status = StageWisePaymentBatchStatus.PendingApproval;
            }
            else if (batch.Status == StageWisePaymentBatchStatus.Draft)
            {
                batch.Status = StageWisePaymentBatchStatus.Approved;
            }
        }

        if (linkedStagePayment is null)
            return (false, "No payment was created.", null);

        foreach (var snapshot in lineSnapshots.OrderBy(s => s.Index))
        {
            var lineReq = snapshot.Line;
            var line = new StageWisePaymentBatchLine
            {
                ApInvoiceDocEntry = snapshot.ApInvoiceDocEntry,
                Bank = lineReq.Bank ?? banks[0]!,
                Amount = lineReq.Amount,
                BalanceDue = snapshot.AdjustedBalanceDue,
                Payable = snapshot.AdjustedPayable,
                LineOrder = snapshot.Index,
                Notes = lineReq.Notes,
            };

            foreach (var termId in lineReq.PaymentTermsTypes.Distinct())
            {
                var term = pageData.PaymentTerms.FirstOrDefault(t => t.Id == termId);
                line.PaymentTerms.Add(new StageWisePaymentBatchLinePaymentTerm
                {
                    PaymentTermsType = termId,
                    PaymentTermDesc = term?.Desc ?? term?.DropDownValue(),
                });
            }

            batch.Lines.Add(line);
        }

        batch.StageWisePaymentId = linkedStagePayment.Id;
        if (downPaymentStageWisePaymentId.HasValue
            && downPaymentStageWisePaymentId.Value != linkedStagePayment.Id)
        {
            batch.DownPaymentStageWisePaymentId = downPaymentStageWisePaymentId;
        }
        batch.ApprovalRequestId = batchApprovalId ?? linkedStagePayment.ApprovalRequestId;

        await db.StageWisePaymentBatches.AddAsync(batch, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var response = await MapBatchAsync(batch.Id, readOnly: true, approvalRequestId: null, cancellationToken);
        return (true, "Batch payment request created.", response);
    }

    public async Task<StageWisePaymentBatchResponse?> GetBatchAsync(int batchId, CancellationToken cancellationToken = default)
    {
        var batch = await db.StageWisePaymentBatches
            .AsNoTracking()
            .Include(b => b.Lines).ThenInclude(l => l.PaymentTerms)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.CompanyDb == CompanyDb, cancellationToken);
        if (batch is null)
            return null;

        var readOnly = batch.Status is not StageWisePaymentBatchStatus.Draft;
        return await MapBatchAsync(batchId, readOnly, null, cancellationToken);
    }

    public async Task<StageWisePaymentBatchResponse?> GetBatchByApprovalRequestIdAsync(
        int approvalRequestId,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.StageWisePaymentBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.CompanyDb == CompanyDb && b.ApprovalRequestId == approvalRequestId.ToString(), cancellationToken);
        if (batch is null)
            return null;

        return await MapBatchAsync(batch.Id, readOnly: true, approvalRequestId, cancellationToken);
    }

    public async Task<StageWisePaymentBatchResponse?> GetBatchByStageWisePaymentIdAsync(
        int stageWisePaymentId,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.StageWisePaymentBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.CompanyDb == CompanyDb
                && (b.StageWisePaymentId == stageWisePaymentId
                    || b.DownPaymentStageWisePaymentId == stageWisePaymentId), cancellationToken);
        if (batch is null)
            return null;

        return await MapBatchAsync(batch.Id, readOnly: true, approvalRequestId: null, cancellationToken);
    }

    public async Task<(bool Success, string Message, IReadOnlyList<(bool Success, string Message)> Operations)> CancelBatchAsync(
        int batchId,
        CancellationToken cancellationToken = default)
    {
        var operations = new List<(bool Success, string Message)>();
        var batch = await db.StageWisePaymentBatches
            .FirstOrDefaultAsync(b => b.Id == batchId && b.CompanyDb == CompanyDb, cancellationToken);

        if (batch is null)
        {
            operations.Add((false, "Batch payment not found."));
            return (false, "Batch payment not found.", operations);
        }

        if (batch.Status == StageWisePaymentBatchStatus.Cancelled)
        {
            operations.Add((false, "Batch payment is already cancelled."));
            return (false, "Batch payment is already cancelled.", operations);
        }

        if (batch.Status == StageWisePaymentBatchStatus.Rejected)
        {
            operations.Add((false, "Rejected batch payments cannot be cancelled."));
            return (false, "Rejected batch payments cannot be cancelled.", operations);
        }

        var paymentIds = await CollectLinkedPaymentIdsAsync(batch, cancellationToken);

        if (paymentIds.Count == 0)
        {
            operations.Add((false, "No linked payment records found for this batch."));
            return (false, "No linked payment records found for this batch.", operations);
        }

        var allSucceeded = true;
        foreach (var paymentId in paymentIds)
        {
            var payment = await db.StageWisePayments
                .FirstOrDefaultAsync(x => x.Id == paymentId && x.CompanyDb == CompanyDb, cancellationToken);
            if (payment is null)
            {
                operations.Add((false, $"Payment record {paymentId} not found."));
                allSucceeded = false;
                continue;
            }

            if (payment.Status == StageWisePaymentStatus.Cancelled)
            {
                operations.Add((true, $"Payment record {paymentId} is already cancelled."));
                continue;
            }

            var (success, paymentOperations) = await stageWisePaymentService.CancelOutgoingPayment(payment, syncBatchStatus: false);
            operations.AddRange(paymentOperations);
            if (!success)
                allSucceeded = false;
        }

        if (!allSucceeded)
        {
            operations.Add((false, "Batch cancellation failed. Some SAP documents may still be active."));
            return (false, "Batch cancellation failed.", operations);
        }

        batch.Status = StageWisePaymentBatchStatus.Cancelled;
        batch.LastModifiedOn = DateTime.UtcNow;
        db.StageWisePaymentBatches.Update(batch);
        await db.SaveChangesAsync(cancellationToken);

        operations.Add((true, "Batch payment cancelled successfully."));
        return (true, "Batch payment cancelled successfully.", operations);
    }

    public async Task<(bool Success, string Message)> DeleteBatchAsync(
        int batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.StageWisePaymentBatches
            .FirstOrDefaultAsync(b => b.Id == batchId && b.CompanyDb == CompanyDb, cancellationToken);

        if (batch is null)
            return (false, "Batch payment not found.");

        if (batch.Status == StageWisePaymentBatchStatus.Cancelled)
            return (false, "Cancelled batch payments cannot be deleted.");

        var paymentIds = await CollectLinkedPaymentIdsAsync(batch, cancellationToken);
        foreach (var paymentId in paymentIds)
        {
            var payment = await db.StageWisePayments
                .FirstOrDefaultAsync(x => x.Id == paymentId && x.CompanyDb == CompanyDb, cancellationToken);
            if (payment is null)
                continue;

            var (success, message) = await stageWisePaymentService.DeleteStageWisePayment(payment);
            if (!success)
                return (false, message);
        }

        db.StageWisePaymentBatches.Remove(batch);
        await db.SaveChangesAsync(cancellationToken);
        return (true, "Batch payment deleted successfully.");
    }

    public async Task<StageWisePayment?> GetPrimaryPaymentRecordAsync(
        int batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.StageWisePaymentBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == batchId && b.CompanyDb == CompanyDb, cancellationToken);
        if (batch?.StageWisePaymentId is null)
            return null;

        return await db.StageWisePayments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == batch.StageWisePaymentId && x.CompanyDb == CompanyDb, cancellationToken);
    }

    async Task<List<int>> CollectLinkedPaymentIdsAsync(
        StageWisePaymentBatch batch,
        CancellationToken cancellationToken)
    {
        var paymentIds = new List<int>();
        if (batch.DownPaymentStageWisePaymentId.HasValue)
            paymentIds.Add(batch.DownPaymentStageWisePaymentId.Value);
        if (batch.StageWisePaymentId.HasValue && !paymentIds.Contains(batch.StageWisePaymentId.Value))
            paymentIds.Add(batch.StageWisePaymentId.Value);

        if (paymentIds.Count <= 1 && batch.StageWisePaymentId.HasValue && batch.DocNumber.HasValue)
        {
            var linkedPayment = await db.StageWisePayments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == batch.StageWisePaymentId && x.CompanyDb == CompanyDb, cancellationToken);
            if (linkedPayment?.StageDesc == "Batch AP payment")
            {
                var orphanDownPayment = await db.StageWisePayments
                    .Where(x => x.CompanyDb == CompanyDb
                        && x.DocNumber == batch.DocNumber
                        && x.StageDesc == "Batch down payment"
                        && x.Status != StageWisePaymentStatus.Cancelled
                        && x.CreatedOn >= batch.CreatedOn.AddMinutes(-1)
                        && x.CreatedOn <= batch.CreatedOn.AddMinutes(10))
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (orphanDownPayment is not null && !paymentIds.Contains(orphanDownPayment.Id))
                    paymentIds.Insert(0, orphanDownPayment.Id);
            }
        }

        return paymentIds;
    }

    private async Task<StageWisePaymentBatchResponse?> MapBatchAsync(
        int batchId,
        bool readOnly,
        int? approvalRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.StageWisePaymentBatches
            .AsNoTracking()
            .Include(b => b.Lines).ThenInclude(l => l.PaymentTerms)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.CompanyDb == CompanyDb, cancellationToken);
        if (batch is null)
            return null;

        var pageData = await pageService.LoadPageDataAsync(batch.PoDocEntry, cancellationToken);
        var canAct = false;
        var isLast = false;
        var linkedPaymentIds = await CollectLinkedPaymentIdsAsync(batch, cancellationToken);
        var linkedPayments = linkedPaymentIds.Count == 0
            ? []
            : await db.StageWisePayments
                .AsNoTracking()
                .Where(x => x.CompanyDb == CompanyDb && linkedPaymentIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
        var canDelete = batch.Status != StageWisePaymentBatchStatus.Cancelled
            && linkedPayments.All(p => string.IsNullOrWhiteSpace(p.ApDownPaymentInvoiceEntryNumber));

        if (approvalRequestId.HasValue)
        {
            var userId = httpContextAccessor.GetUserIdAsync();
            var approval = await db.ApprovalRequests
                .Include(r => r.UserApprovals)
                .FirstOrDefaultAsync(r => r.Id == approvalRequestId && r.CompanyDb == CompanyDb, cancellationToken);
            if (approval is not null && userId.HasValue)
            {
                var pending = approval.UserApprovals
                    .Where(u => u.ApprovalStatus == ApprovalStatus.Pending)
                    .OrderBy(u => u.Priority)
                    .FirstOrDefault();
                canAct = pending?.UserId == userId;
                isLast = pending is not null
                    && !approval.UserApprovals.Any(u =>
                        u.ApprovalStatus == ApprovalStatus.Pending && u.Priority > pending.Priority);
            }
        }

        return new StageWisePaymentBatchResponse
        {
            Id = batch.Id,
            PoDocEntry = batch.PoDocEntry,
            DocNumber = batch.DocNumber,
            StageWisePaymentId = batch.StageWisePaymentId,
            DownPaymentStageWisePaymentId = batch.DownPaymentStageWisePaymentId,
            ApprovalRequestId = batch.ApprovalRequestId,
            ApprovalRequestIdNumeric = int.TryParse(batch.ApprovalRequestId, out var arId) ? arId : null,
            Status = batch.Status.ToString(),
            ReadOnly = readOnly,
            CanCancel = batch.Status is StageWisePaymentBatchStatus.Approved or StageWisePaymentBatchStatus.PendingApproval,
            CanDelete = canDelete,
            CanApprove = canAct,
            CanReject = canAct,
            IsLastApproval = isLast,
            WtCode = batch.WtCode,
            ModeOfPayment = batch.ModeOfPayment,
            ModeOfPaymentLabel = Constants.SapPaymentMeansType.Labels.GetValueOrDefault(batch.ModeOfPayment ?? string.Empty),
            Account = batch.Account,
            AccountLabel = pageData?.BankLabels.GetValueOrDefault(batch.Account ?? string.Empty, batch.Account),
            JournalRemark = batch.JournalRemark,
            ReferenceNo = batch.ReferenceNo,
            PostingDate = batch.PostingDate,
            PaymentDate = batch.PaymentDate,
            Lines = batch.Lines.OrderBy(l => l.LineOrder).Select(line =>
            {
                var ap = string.IsNullOrWhiteSpace(line.ApInvoiceDocEntry)
                    ? null
                    : pageData?.ApInvoices.FirstOrDefault(x => x.DocEntry.ToString() == line.ApInvoiceDocEntry);
                return new StageWisePaymentBatchLineResponse
                {
                    Id = line.Id,
                    ApInvoiceDocEntry = line.ApInvoiceDocEntry,
                    ApInvoiceLabel = ap is null
                        ? (string.IsNullOrWhiteSpace(line.ApInvoiceDocEntry) ? "—" : line.ApInvoiceDocEntry)
                        : $"{ap.DocNum}:{ap.NumAtCard}",
                    PaymentTermsTypes = line.PaymentTerms.Select(t => t.PaymentTermsType).ToList(),
                    PaymentTermLabels = line.PaymentTerms.Select(t => t.PaymentTermDesc ?? $"Term {t.PaymentTermsType}").ToList(),
                    Bank = line.Bank,
                    Amount = line.Amount,
                    BalanceDue = line.BalanceDue ?? 0,
                    Payable = line.Payable ?? 0,
                    Notes = line.Notes,
                };
            }).ToList(),
        };
    }

    private async Task<List<StageWisePayment>> GetActiveRecordsAsync(int docNumber, CancellationToken cancellationToken) =>
        await db.StageWisePayments
            .AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb && x.DocNumber == docNumber && x.Status != StageWisePaymentStatus.Cancelled)
            .ToListAsync(cancellationToken);

    private sealed record BatchLineSnapshot(
        StageWisePaymentBatchLineRequest Line,
        int Index,
        bool RequiresAp,
        double AdjustedBalanceDue,
        double AdjustedPayable,
        string? ApInvoiceDocEntry);

    private static void ApplyAdditionalDetailsToVendorPayment(
        SapVendorPaymentRequests request,
        CreateStageWisePaymentBatchRequest batchRequest,
        string fallbackAccount,
        double amount)
    {
        var account = batchRequest.Account ?? fallbackAccount;
        var mode = batchRequest.ModeOfPayment ?? Constants.SapPaymentMeansType.BankTransfer;
        var paymentDate = batchRequest.PaymentDate ?? DateTime.UtcNow;
        var postingDate = batchRequest.PostingDate ?? paymentDate;

        request.TransferDate = paymentDate;
        request.DocDate = paymentDate;
        request.DocDueDate = paymentDate;
        request.PostingDate = postingDate;
        request.TransferReference = batchRequest.ReferenceNo ?? string.Empty;
        request.CounterReference = batchRequest.ReferenceNo ?? string.Empty;
        request.JournalRemarks = batchRequest.JournalRemark;

        switch (mode)
        {
            case Constants.SapPaymentMeansType.Cash:
                request.CashAccount = account;
                break;
            case Constants.SapPaymentMeansType.Check:
                request.CheckAccount = account;
                break;
            default:
                request.TransferAccount = account;
                break;
        }

        request.CashFlowAssignments =
        [
            new CashFlowAssignments
            {
                AmountLc = amount.ToString("F2"),
                PaymentMeans = mode,
            },
        ];
    }
}
