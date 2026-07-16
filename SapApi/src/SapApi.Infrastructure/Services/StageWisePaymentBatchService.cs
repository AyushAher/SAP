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
    IUnitOfWork unitOfWork,
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
        var activeRecords = await GetActiveRecordsForCalculationsAsync(
            pageData.PurchaseOrder.DocNum ?? request.PoDocEntry,
            pageData.PaymentTerms,
            cancellationToken);
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

        var activeRecords = await GetActiveRecordsForCalculationsAsync(
            request.DocNumber ?? po.DocNum ?? request.PoDocEntry,
            pageData.PaymentTerms,
            cancellationToken);
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
                var (balanceDue, _) = StageWisePaymentCalculations.ResolveBatchRowAmounts(
                    po, pageData.PaymentTerms, activeRecords, line.PaymentTermsTypes, apInvoice,
                    line.ApInvoiceDocEntry, pageData.TotalBasic);

                var priorInBatch = request.Lines
                    .Take(index)
                    .Where(l => l.ApInvoiceDocEntry == line.ApInvoiceDocEntry)
                    .Sum(l => l.Amount);
                var adjustedBalanceDue = Math.Round(Math.Max(0, balanceDue - priorInBatch), 2);
                var adjustedPayable = StageWisePaymentCalculations.ComputeSequentialStageRowPayable(
                    line, priorLines, po, pageData.PaymentTerms, activeRecords, pageData.TotalBasic);

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
            PostingDate = DateTimeUtcConverter.ToUtc(request.PostingDate),
            PaymentDate = DateTimeUtcConverter.ToUtc(request.PaymentDate),
            Status = StageWisePaymentBatchStatus.Draft,
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = DateTime.UtcNow,
        };

        StageWisePayment? linkedStagePayment = null;
        StageWisePayment? downPaymentEntity = null;
        int? downPaymentStageWisePaymentId = null;
        string? batchApprovalId = null;

        // SAP Service Layer calls first — never hold a DB transaction across HTTP.
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
            ApplyAdditionalDetailsToVendorPayment(
                vendorRequest, request, banks[0]!, po.BPLId, po.DocNum?.ToString());

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
            var (success, message, payment) = await stageWisePaymentService.CreateBatchDownPaymentAsync(
                po,
                downPaymentLines,
                pageData.PaymentTerms,
                pageData.TotalBasic,
                banks[0],
                request.WtCode,
                activeRecords,
                userRemark: request.JournalRemark,
                persist: false,
                cancellationToken);

            if (!success || payment is null)
                return (false, message, null);

            downPaymentEntity = payment;
            if (linkedStagePayment is null)
                linkedStagePayment = payment;

            if (payment.Status == StageWisePaymentStatus.PendingApproval)
            {
                batchApprovalId ??= payment.ApprovalRequestId;
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

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (linkedStagePayment is not null && linkedStagePayment.Id == 0)
                    await db.StageWisePayments.AddAsync(linkedStagePayment, ct);

                if (downPaymentEntity is not null
                    && downPaymentEntity.Id == 0
                    && !ReferenceEquals(downPaymentEntity, linkedStagePayment))
                {
                    await db.StageWisePayments.AddAsync(downPaymentEntity, ct);
                }

                await db.SaveChangesAsync(ct);

                batch.StageWisePaymentId = linkedStagePayment!.Id;
                if (downPaymentEntity is not null && downPaymentEntity.Id != linkedStagePayment.Id)
                    batch.DownPaymentStageWisePaymentId = downPaymentEntity.Id;
                batch.ApprovalRequestId = batchApprovalId ?? linkedStagePayment.ApprovalRequestId;

                await db.StageWisePaymentBatches.AddAsync(batch, ct);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            return (false, $"SAP payment succeeded but failed to save batch locally: {ex.Message}", null);
        }

        var response = await MapBatchAsync(batch.Id, readOnly: true, approvalRequestId: null, cancellationToken);
        return (true, "Batch payment request created.", response);
    }

    public async Task<(bool Success, string Message, StageWisePaymentBatchResponse? Data)> WithdrawBatchApprovalAsync(
        int batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.StageWisePaymentBatches
            .Include(b => b.Lines).ThenInclude(l => l.PaymentTerms)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.CompanyDb == CompanyDb, cancellationToken);
        if (batch is null)
            return (false, "Batch payment not found.", null);

        if (batch.Status is not (StageWisePaymentBatchStatus.PendingApproval or StageWisePaymentBatchStatus.Rejected))
            return (false, "Only pending or rejected batch approval requests can be withdrawn.", null);

        var paymentIds = await CollectLinkedPaymentIdsAsync(batch, cancellationToken);
        var linkedPayments = paymentIds.Count == 0
            ? []
            : await db.StageWisePayments
                .Where(x => x.CompanyDb == CompanyDb && paymentIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

        if (linkedPayments.Any(p => !string.IsNullOrWhiteSpace(p.ApDownPaymentInvoiceEntryNumber)))
            return (false, "Cannot withdraw: SAP documents already exist. Cancel the payment instead.", null);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                foreach (var payment in linkedPayments)
                {
                    var approvalIds = payment.ApprovalRequestId?
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList() ?? [];
                    if (approvalIds.Count > 0)
                    {
                        var approvalRequests = await db.ApprovalRequests
                            .Where(x => x.CompanyDb == CompanyDb && approvalIds.Contains(x.Id.ToString()))
                            .ToListAsync(ct);
                        db.ApprovalRequests.RemoveRange(approvalRequests);
                    }

                    payment.ApprovalRequestId = null;
                    payment.GrossAmount = 0;
                    payment.GstAmount = 0;
                    payment.Tds = 0;
                    payment.Status = StageWisePaymentStatus.Cancelled;
                    payment.LastModifiedOn = DateTime.UtcNow;
                    db.StageWisePayments.Update(payment);
                }

                batch.ApprovalRequestId = null;
                batch.Status = StageWisePaymentBatchStatus.Draft;
                batch.LastModifiedOn = DateTime.UtcNow;
                db.StageWisePaymentBatches.Update(batch);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to withdraw approval request: {ex.Message}", null);
        }

        var response = await MapBatchAsync(batch.Id, readOnly: false, approvalRequestId: null, cancellationToken);
        return (true, "Approval request withdrawn. You can edit and submit again.", response);
    }

    public async Task<(bool Success, string Message, StageWisePaymentBatchResponse? Data)> UpdateAdditionalDetailsAsync(
        int batchId,
        UpdateBatchAdditionalDetailsRequest request,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.StageWisePaymentBatches
            .FirstOrDefaultAsync(b => b.Id == batchId && b.CompanyDb == CompanyDb, cancellationToken);
        if (batch is null)
            return (false, "Batch payment not found.", null);

        if (batch.Status is StageWisePaymentBatchStatus.Cancelled or StageWisePaymentBatchStatus.Rejected)
            return (false, "Additional details cannot be edited for cancelled or rejected batches.", null);

        if (batch.Status == StageWisePaymentBatchStatus.PendingApproval)
        {
            if (!int.TryParse(batch.ApprovalRequestId, out var approvalRequestId))
                return (false, "Additional details cannot be edited while approval is pending.", null);

            var mapped = await MapBatchAsync(batch.Id, readOnly: true, approvalRequestId, cancellationToken);
            if (mapped is null || !mapped.CanEditAdditionalDetails)
                return (false, "Only the current approver can edit additional details while approval is pending.", null);
        }

        var linkedPaymentIds = await CollectLinkedPaymentIdsAsync(batch, cancellationToken);
        var hasSapOutgoingPayment = linkedPaymentIds.Count > 0
            && await db.StageWisePayments
                .AsNoTracking()
                .Where(x => x.CompanyDb == CompanyDb && linkedPaymentIds.Contains(x.Id))
                .AnyAsync(x =>
                    !string.IsNullOrWhiteSpace(x.PaymentDocEntry)
                    || (x.StageDesc == "Batch AP payment"
                        && !string.IsNullOrWhiteSpace(x.ApDownPaymentInvoiceEntryNumber)),
                    cancellationToken);

        if (!hasSapOutgoingPayment && string.IsNullOrWhiteSpace(request.Account))
            return (false, "Please select an account.", null);

        if (request.PostingDate is null || (!hasSapOutgoingPayment && request.PaymentDate is null))
            return (false, "Posting date and payment date are required.", null);

        if (!hasSapOutgoingPayment)
        {
            batch.ModeOfPayment = request.ModeOfPayment ?? Constants.SapPaymentMeansType.BankTransfer;
            batch.Account = request.Account;
            batch.ReferenceNo = request.ReferenceNo;
            batch.PaymentDate = DateTimeUtcConverter.ToUtc(request.PaymentDate);
        }

        batch.JournalRemark = request.JournalRemark;
        batch.PostingDate = DateTimeUtcConverter.ToUtc(request.PostingDate);
        batch.LastModifiedOn = DateTime.UtcNow;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(_ =>
            {
                db.StageWisePaymentBatches.Update(batch);
                return Task.CompletedTask;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to save additional details: {ex.Message}", null);
        }

        var readOnly = batch.Status is not StageWisePaymentBatchStatus.Draft;
        int? approvalRequestIdForMap = int.TryParse(batch.ApprovalRequestId, out var arId) ? arId : null;
        var response = await MapBatchAsync(
            batch.Id,
            readOnly,
            batch.Status == StageWisePaymentBatchStatus.PendingApproval ? approvalRequestIdForMap : null,
            cancellationToken);
        return (true, "Additional details saved.", response);
    }

    public async Task<(bool Success, string Message, StageWisePaymentBatchResponse? Data)> UpdateBatchAsync(
        int batchId,
        CreateStageWisePaymentBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.StageWisePaymentBatches
            .Include(b => b.Lines).ThenInclude(l => l.PaymentTerms)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.CompanyDb == CompanyDb, cancellationToken);
        if (batch is null)
            return (false, "Batch payment not found.", null);

        if (batch.Status != StageWisePaymentBatchStatus.Draft)
            return (false, "Only draft batches can be edited. Withdraw the approval request first.", null);

        var linkedPaymentIds = await CollectLinkedPaymentIdsAsync(batch, cancellationToken);
        if (linkedPaymentIds.Count > 0)
        {
            var activeLinked = await db.StageWisePayments
                .AsNoTracking()
                .AnyAsync(x => x.CompanyDb == CompanyDb
                    && linkedPaymentIds.Contains(x.Id)
                    && x.Status != StageWisePaymentStatus.Cancelled, cancellationToken);
            if (activeLinked)
                return (false, "Batch still has active payment records. Withdraw the approval request first.", null);
        }

        var (validated, validationMessage, pageData, lineSnapshots, banks) =
            await ValidateBatchRequestAsync(request, cancellationToken);
        if (!validated || pageData?.PurchaseOrder is null)
            return (false, validationMessage, null);

        ApplyBatchHeader(batch, request, pageData.PurchaseOrder.DocNum);
        ReplaceBatchLines(batch, lineSnapshots, banks[0]!, pageData);

        batch.LastModifiedOn = DateTime.UtcNow;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(_ =>
            {
                db.StageWisePaymentBatches.Update(batch);
                return Task.CompletedTask;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to update batch: {ex.Message}", null);
        }

        var response = await MapBatchAsync(batch.Id, readOnly: false, approvalRequestId: null, cancellationToken);
        return (true, "Batch payment updated.", response);
    }

    public async Task<(bool Success, string Message, StageWisePaymentBatchResponse? Data)> SubmitBatchAsync(
        int batchId,
        CreateStageWisePaymentBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var (updated, updateMessage, _) = await UpdateBatchAsync(batchId, request, cancellationToken);
        if (!updated)
            return (false, updateMessage, null);

        var batch = await db.StageWisePaymentBatches
            .Include(b => b.Lines).ThenInclude(l => l.PaymentTerms)
            .FirstOrDefaultAsync(b => b.Id == batchId && b.CompanyDb == CompanyDb, cancellationToken);
        if (batch is null)
            return (false, "Batch payment not found.", null);

        var cleanupIds = await CollectLinkedPaymentIdsAsync(batch, cancellationToken);
        if (cleanupIds.Count > 0)
        {
            var stalePayments = await db.StageWisePayments
                .Where(x => x.CompanyDb == CompanyDb && cleanupIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
            foreach (var payment in stalePayments)
            {
                if (payment.Status != StageWisePaymentStatus.Cancelled
                    || !string.IsNullOrWhiteSpace(payment.ApDownPaymentInvoiceEntryNumber))
                {
                    return (false, "Cannot resubmit while active SAP-linked payments remain.", null);
                }

                var (deleted, message) = await stageWisePaymentService.DeleteStageWisePayment(payment);
                if (!deleted)
                    return (false, message, null);
            }

            try
            {
                await unitOfWork.ExecuteInTransactionAsync(_ =>
                {
                    batch.StageWisePaymentId = null;
                    batch.DownPaymentStageWisePaymentId = null;
                    db.StageWisePaymentBatches.Update(batch);
                    return Task.CompletedTask;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to clear stale payment links: {ex.Message}", null);
            }
        }

        var (validated, validationMessage, pageData, lineSnapshots, banks) =
            await ValidateBatchRequestAsync(request, cancellationToken);
        if (!validated || pageData?.PurchaseOrder is null)
            return (false, validationMessage, null);

        var po = pageData.PurchaseOrder;
        var activeRecords = await GetActiveRecordsForCalculationsAsync(
            request.DocNumber ?? po.DocNum ?? request.PoDocEntry,
            pageData.PaymentTerms,
            cancellationToken);
        var apInvoicesByDocEntry = pageData.ApInvoices
            .Where(x => x.DocEntry.HasValue)
            .ToDictionary(x => x.DocEntry!.Value.ToString(), x => x, StringComparer.Ordinal);

        var (linkSuccess, linkMessage, linkedStagePayment, _, _, _) =
            await CreateLinkedPaymentsAsync(
                request,
                po,
                pageData,
                lineSnapshots,
                banks[0]!,
                activeRecords,
                apInvoicesByDocEntry,
                batch.DocNumber,
                cancellationToken,
                batch);

        if (!linkSuccess || linkedStagePayment is null)
            return (false, linkMessage, null);

        var response = await MapBatchAsync(batch.Id, readOnly: true, approvalRequestId: null, cancellationToken);
        return (true, "Batch payment submitted for approval.", response);
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

        var readOnly = batch.Status is not StageWisePaymentBatchStatus.Draft;
        return await MapBatchAsync(batch.Id, readOnly, approvalRequestId: null, cancellationToken);
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

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(_ =>
            {
                batch.Status = StageWisePaymentBatchStatus.Cancelled;
                batch.LastModifiedOn = DateTime.UtcNow;
                db.StageWisePaymentBatches.Update(batch);
                return Task.CompletedTask;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            operations.Add((false, $"SAP cancel succeeded but failed to update batch status: {ex.Message}"));
            return (false, $"Failed to update batch status: {ex.Message}", operations);
        }

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

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(_ =>
            {
                db.StageWisePaymentBatches.Remove(batch);
                return Task.CompletedTask;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to delete batch: {ex.Message}");
        }

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
        var canWithdraw = canDelete
            && batch.Status is StageWisePaymentBatchStatus.PendingApproval or StageWisePaymentBatchStatus.Rejected;
        var canSubmit = batch.Status == StageWisePaymentBatchStatus.Draft
            && linkedPayments.All(p => p.Status == StageWisePaymentStatus.Cancelled);
        var hasSapOutgoingPayment = linkedPayments.Any(p =>
            !string.IsNullOrWhiteSpace(p.PaymentDocEntry)
            || (p.StageDesc == "Batch AP payment"
                && !string.IsNullOrWhiteSpace(p.ApDownPaymentInvoiceEntryNumber)));

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

        // Editable when draft, when payment was posted without a pending approval queue,
        // or when the current user is the active approver for this request.
        var canEditAdditionalDetails = batch.Status is StageWisePaymentBatchStatus.Draft or StageWisePaymentBatchStatus.Approved
            || canAct;

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
            CanWithdraw = canWithdraw,
            CanSubmit = canSubmit,
            CanEditAdditionalDetails = canEditAdditionalDetails,
            HasSapOutgoingPayment = hasSapOutgoingPayment,
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

    private async Task<List<StageWisePayment>> GetActiveRecordsForCalculationsAsync(
        int docNumber,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        CancellationToken cancellationToken)
    {
        var activeRecords = await db.StageWisePayments
            .AsNoTracking()
            .Where(x => x.CompanyDb == CompanyDb && x.DocNumber == docNumber && x.Status != StageWisePaymentStatus.Cancelled)
            .ToListAsync(cancellationToken);

        var batchPaymentIds = activeRecords
            .Where(StageWisePaymentCalculations.IsBatchPaymentRecord)
            .Where(x => x.PaymentTermsType is null)
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        if (batchPaymentIds.Count == 0)
            return activeRecords;

        var batches = await db.StageWisePaymentBatches
            .AsNoTracking()
            .Include(b => b.Lines).ThenInclude(l => l.PaymentTerms)
            .Where(b => b.CompanyDb == CompanyDb
                && ((b.StageWisePaymentId != null && batchPaymentIds.Contains(b.StageWisePaymentId.Value))
                    || (b.DownPaymentStageWisePaymentId != null
                        && batchPaymentIds.Contains(b.DownPaymentStageWisePaymentId.Value))))
            .ToListAsync(cancellationToken);

        var termMap = new Dictionary<int, IReadOnlyList<int>>();
        foreach (var batch in batches)
        {
            var termIds = batch.Lines
                .SelectMany(l => l.PaymentTerms)
                .Select(t => t.PaymentTermsType)
                .Distinct()
                .ToList();

            if (batch.StageWisePaymentId is int primaryId && batchPaymentIds.Contains(primaryId))
                termMap[primaryId] = termIds;
            if (batch.DownPaymentStageWisePaymentId is int dpId && batchPaymentIds.Contains(dpId))
                termMap[dpId] = termIds;
        }

        return StageWisePaymentCalculations.ExpandActiveRecordsForTermCalculations(
            activeRecords, termMap, paymentTerms);
    }

    private async Task<(
        bool Success,
        string Message,
        StageWisePaymentPageDataResponse? PageData,
        List<BatchLineSnapshot> LineSnapshots,
        List<string?> Banks)> ValidateBatchRequestAsync(
        CreateStageWisePaymentBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0)
            return (false, "Add at least one payment row.", null, [], []);

        var pageData = await pageService.LoadPageDataAsync(request.PoDocEntry, cancellationToken);
        if (pageData?.PurchaseOrder is null)
            return (false, "Purchase order not found.", null, [], []);

        var po = pageData.PurchaseOrder;
        var banks = request.Lines.Select(l => l.Bank).Distinct().ToList();
        if (banks.Count != 1 || string.IsNullOrWhiteSpace(banks[0]))
            return (false, "All rows must use the same bank account.", null, [], []);

        if (string.IsNullOrWhiteSpace(request.Account))
            return (false, "Please select an account.", null, [], []);

        if (request.PostingDate is null || request.PaymentDate is null)
            return (false, "Posting date and payment date are required.", null, [], []);

        var activeRecords = await GetActiveRecordsForCalculationsAsync(
            request.DocNumber ?? po.DocNum ?? request.PoDocEntry,
            pageData.PaymentTerms,
            cancellationToken);
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
            return (false, validationMessage, null, [], []);

        var (compositionValid, compositionMessage) = StageWisePaymentCalculations.ValidateBatchComposition(
            po, pageData.PaymentTerms, request.Lines);
        if (!compositionValid)
            return (false, compositionMessage, null, [], []);

        var lineSnapshots = new List<BatchLineSnapshot>();
        var tdsAppliedInBatch = new HashSet<string>(StringComparer.Ordinal);
        var priorLines = new List<StageWisePaymentBatchLineRequest>();

        foreach (var (line, index) in request.Lines.Select((line, index) => (line, index)))
        {
            var requiresAp = StageWisePaymentCalculations.BatchRowRequiresApInvoice(
                po, pageData.PaymentTerms, line.PaymentTermsTypes);

            if (requiresAp)
            {
                var apInvoice = pageData.ApInvoices.First(x => x.DocEntry.ToString() == line.ApInvoiceDocEntry);
                var (balanceDue, _) = StageWisePaymentCalculations.ResolveBatchRowAmounts(
                    po, pageData.PaymentTerms, activeRecords, line.PaymentTermsTypes, apInvoice,
                    line.ApInvoiceDocEntry, pageData.TotalBasic);

                var priorInBatch = request.Lines
                    .Take(index)
                    .Where(l => l.ApInvoiceDocEntry == line.ApInvoiceDocEntry)
                    .Sum(l => l.Amount);
                var adjustedBalanceDue = Math.Round(Math.Max(0, balanceDue - priorInBatch), 2);
                var adjustedPayable = StageWisePaymentCalculations.ComputeSequentialStageRowPayable(
                    line, priorLines, po, pageData.PaymentTerms, activeRecords, pageData.TotalBasic);

                var hadTdsDeducted = activeRecords.Any(x =>
                    x.ApInvoiceDocEntry == line.ApInvoiceDocEntry && (x.Tds ?? 0) != 0)
                    || !tdsAppliedInBatch.Add(line.ApInvoiceDocEntry!);
                var net = line.Amount - (hadTdsDeducted ? 0 : apInvoice.WTAmount ?? 0);
                if (net <= 0)
                    return (false, $"Net payment must be greater than zero for AP invoice {apInvoice.DocNum}.", null, [], []);

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

        return (true, string.Empty, pageData, lineSnapshots, banks);
    }

    private static void ApplyBatchHeader(
        StageWisePaymentBatch batch,
        CreateStageWisePaymentBatchRequest request,
        int? poDocNum)
    {
        batch.PoDocEntry = request.PoDocEntry;
        batch.DocNumber = request.DocNumber ?? poDocNum;
        batch.WtCode = request.WtCode;
        batch.ModeOfPayment = request.ModeOfPayment ?? Constants.SapPaymentMeansType.BankTransfer;
        batch.Account = request.Account;
        batch.JournalRemark = request.JournalRemark;
        batch.ReferenceNo = request.ReferenceNo;
        batch.PostingDate = DateTimeUtcConverter.ToUtc(request.PostingDate);
        batch.PaymentDate = DateTimeUtcConverter.ToUtc(request.PaymentDate);
    }

    private static void ReplaceBatchLines(
        StageWisePaymentBatch batch,
        List<BatchLineSnapshot> lineSnapshots,
        string bank,
        StageWisePaymentPageDataResponse? pageData = null)
    {
        batch.Lines.Clear();
        foreach (var snapshot in lineSnapshots.OrderBy(s => s.Index))
        {
            var lineReq = snapshot.Line;
            var line = new StageWisePaymentBatchLine
            {
                ApInvoiceDocEntry = snapshot.ApInvoiceDocEntry,
                Bank = lineReq.Bank ?? bank,
                Amount = lineReq.Amount,
                BalanceDue = snapshot.AdjustedBalanceDue,
                Payable = snapshot.AdjustedPayable,
                LineOrder = snapshot.Index,
                Notes = lineReq.Notes,
            };

            foreach (var termId in lineReq.PaymentTermsTypes.Distinct())
            {
                var term = pageData?.PaymentTerms.FirstOrDefault(t => t.Id == termId);
                line.PaymentTerms.Add(new StageWisePaymentBatchLinePaymentTerm
                {
                    PaymentTermsType = termId,
                    PaymentTermDesc = term?.Desc ?? term?.DropDownValue(),
                });
            }

            batch.Lines.Add(line);
        }
    }

    private async Task<(
        bool Success,
        string Message,
        StageWisePayment? LinkedStagePayment,
        int? DownPaymentStageWisePaymentId,
        string? BatchApprovalId,
        StageWisePaymentBatchStatus BatchStatus)> CreateLinkedPaymentsAsync(
        CreateStageWisePaymentBatchRequest request,
        SapPurchaseOrdersResponse po,
        StageWisePaymentPageDataResponse pageData,
        List<BatchLineSnapshot> lineSnapshots,
        string bank,
        List<StageWisePayment> activeRecords,
        Dictionary<string, SapPurchaseInvoicesResponse> apInvoicesByDocEntry,
        int? batchDocNumber,
        CancellationToken cancellationToken,
        StageWisePaymentBatch? batchToLink = null)
    {
        StageWisePayment? linkedStagePayment = null;
        int? downPaymentStageWisePaymentId = null;
        string? batchApprovalId = null;
        var batchStatus = StageWisePaymentBatchStatus.Draft;

        var paymentInvoices = new List<PaymentInvoice>();
        var totalTransfer = 0.0;
        var lineNumber = 0;
        var tdsAppliedInBatch = new HashSet<string>(StringComparer.Ordinal);

        foreach (var snapshot in lineSnapshots.Where(s => s.RequiresAp).OrderBy(s => s.Index))
        {
            var line = snapshot.Line;
            var apInvoice = pageData.ApInvoices.First(x => x.DocEntry.ToString() == line.ApInvoiceDocEntry);
            var hadTdsDeducted = activeRecords.Any(x =>
                x.ApInvoiceDocEntry == line.ApInvoiceDocEntry && (x.Tds ?? 0) != 0)
                || !tdsAppliedInBatch.Add(line.ApInvoiceDocEntry!);
            var net = line.Amount - (hadTdsDeducted ? 0 : apInvoice.WTAmount ?? 0);
            if (net <= 0)
                return (false, $"Net payment must be greater than zero for AP invoice {apInvoice.DocNum}.", null, null, null, batchStatus);

            totalTransfer += net;
            paymentInvoices.Add(new PaymentInvoice
            {
                LineNumber = lineNumber++,
                DocEntry = apInvoice.DocEntry,
                InvoiceType = Constants.SapVendorPaymentInvoiceType.Invoice,
                SumApplied = net,
                AppliedFC = 0,
            });
        }

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
            ApplyAdditionalDetailsToVendorPayment(
                vendorRequest, request, bank, po.BPLId, po.DocNum?.ToString());

            var sapResponse = await vendorPaymentService.CreateVendorPayments(
                vendorRequest, supportingData: po.DocEntry?.ToString());

            if (sapResponse?.Error?.Message?.Value is not null)
                return (false, $"SAP Error: {sapResponse.Error.Message.Value}", null, null, null, batchStatus);

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
                DocNumber = batchDocNumber ?? request.DocNumber ?? po.DocNum,
                Bank = bank,
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
                batchStatus = StageWisePaymentBatchStatus.PendingApproval;
                batchApprovalId = linkedStagePayment.ApprovalRequestId;
            }
            else if (sapResponse?.DocEntry.HasValue == true)
            {
                linkedStagePayment.ApDownPaymentInvoiceEntryNumber = sapResponse.DocNumber?.ToString();
                linkedStagePayment.PaymentDocEntry = sapResponse.DocEntry?.ToString();
                linkedStagePayment.Status = StageWisePaymentStatus.Added;
                batchStatus = StageWisePaymentBatchStatus.Approved;
            }
            else
            {
                return (false, "No vendor payment was created in SAP.", null, null, null, batchStatus);
            }
        }

        StageWisePayment? downPaymentEntity = null;
        var downPaymentSnapshots = lineSnapshots.Where(s => !s.RequiresAp).ToList();
        if (downPaymentSnapshots.Count > 0)
        {
            foreach (var snapshot in downPaymentSnapshots)
            {
                var term = StageWisePaymentCalculations.ResolveDownPaymentTerm(
                    pageData.PaymentTerms, snapshot.Line.PaymentTermsTypes);
                if (term is null)
                    return (false, "Payment term not found for down payment row.", null, null, null, batchStatus);
            }

            var downPaymentLines = downPaymentSnapshots.Select(s => s.Line).ToList();
            var (success, message, payment) = await stageWisePaymentService.CreateBatchDownPaymentAsync(
                po,
                downPaymentLines,
                pageData.PaymentTerms,
                pageData.TotalBasic,
                bank,
                request.WtCode,
                activeRecords,
                userRemark: request.JournalRemark,
                persist: false,
                cancellationToken);

            if (!success || payment is null)
                return (false, message, null, null, null, batchStatus);

            downPaymentEntity = payment;
            if (linkedStagePayment is null)
                linkedStagePayment = payment;

            if (payment.Status == StageWisePaymentStatus.PendingApproval)
            {
                batchApprovalId ??= payment.ApprovalRequestId;
                batchStatus = StageWisePaymentBatchStatus.PendingApproval;
            }
            else if (batchStatus == StageWisePaymentBatchStatus.Draft)
            {
                batchStatus = StageWisePaymentBatchStatus.Approved;
            }
        }

        if (linkedStagePayment is null)
            return (false, "No payment was created.", null, null, null, batchStatus);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                if (linkedStagePayment.Id == 0)
                    await db.StageWisePayments.AddAsync(linkedStagePayment, ct);

                if (downPaymentEntity is not null
                    && downPaymentEntity.Id == 0
                    && !ReferenceEquals(downPaymentEntity, linkedStagePayment))
                {
                    await db.StageWisePayments.AddAsync(downPaymentEntity, ct);
                }

                if (batchToLink is not null)
                {
                    // Ensure payment Ids exist before linking the batch.
                    await db.SaveChangesAsync(ct);

                    batchToLink.StageWisePaymentId = linkedStagePayment.Id;
                    if (downPaymentEntity is not null && downPaymentEntity.Id != linkedStagePayment.Id)
                        batchToLink.DownPaymentStageWisePaymentId = downPaymentEntity.Id;
                    else
                        batchToLink.DownPaymentStageWisePaymentId = null;

                    batchToLink.ApprovalRequestId = batchApprovalId ?? linkedStagePayment.ApprovalRequestId;
                    batchToLink.Status = batchStatus;
                    batchToLink.LastModifiedOn = DateTime.UtcNow;
                    db.StageWisePaymentBatches.Update(batchToLink);
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            return (false, $"SAP payment succeeded but failed to save locally: {ex.Message}", null, null, null, batchStatus);
        }

        downPaymentStageWisePaymentId = downPaymentEntity?.Id;
        return (true, string.Empty, linkedStagePayment, downPaymentStageWisePaymentId, batchApprovalId, batchStatus);
    }

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
        int? bplId,
        string? poNumber)
    {
        var account = batchRequest.Account ?? fallbackAccount;
        var mode = batchRequest.ModeOfPayment ?? Constants.SapPaymentMeansType.BankTransfer;
        var paymentDate = DateTimeUtcConverter.ToUtc(batchRequest.PaymentDate) ?? DateTime.UtcNow;
        var postingDate = DateTimeUtcConverter.ToUtc(batchRequest.PostingDate) ?? paymentDate;

        request.TransferDate = paymentDate;
        request.DocDate = paymentDate;
        request.DocDueDate = paymentDate;
        request.PostingDate = postingDate;
        request.TransferReference = batchRequest.ReferenceNo ?? string.Empty;
        request.CounterReference = batchRequest.ReferenceNo ?? string.Empty;
        request.Remarks = Constants.PaymentRemarks.Build(batchRequest.JournalRemark, bplId, poNumber);
        request.JournalRemarks = null;

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

        // Do not send CashFlowAssignments with CashFlowLineItemID=0 — SAP returns
        // "Invalid cash flow primary item [Message 3741-3]". Match AddOutgoingPayment /
        // SapForms and omit cash-flow lines unless a valid primary item is known.
        request.CashFlowAssignments = [];
    }
}
