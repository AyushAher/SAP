using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services;

public class StageWisePaymentService(
    SapPurchaseDownPaymentService sapPurchaseDownPaymentService,
    SapVendorPaymentService sapVendorPaymentService,
    AppDbContext context,
    IUnitOfWork unitOfWork,
    ICurrentCompanyDbAccessor companyDbAccessor,
    PurchaseOrders.PurchaseOrderLinkResolver purchaseOrderLinks)
{
    private string CompanyDb => companyDbAccessor.GetCompanyDbName();
    public async Task<(bool IsSuccess, string Message, int? PaymentId)> CreateStageWisePayment(
        StageWisePayment entity,
        SapPurchaseOrdersResponse? purchaseOrder,
        PaymentTermsUdf? selectedPaymentTermsUdf,
        double downPaymentAmount,
        double totalBasic,
        double? payableAmount,
        string? wtCode,
        string? desc,
        List<StageWisePayment> existingRecords)
    {

        if (purchaseOrder is null)
            return (false, "Purchase order not found!", null);

        if (selectedPaymentTermsUdf is null)
            return (false, "Payment term not selected!", null);

        if (StageWisePaymentCalculations.RequiresBatchPayment(purchaseOrder, selectedPaymentTermsUdf, entity.ApInvoiceDocEntry))
            return (false, "AP invoice payments must be created using batch payment.", null);

        if (downPaymentAmount <= 0)
            return (false, "Down payment amount cannot be less than or equal to 0!", null);

        var paidBasicTotal = existingRecords
            .Where(x => x.PaymentTermsType == selectedPaymentTermsUdf.Id)
            .Sum(x => x.GrossAmount);

        var paidGstTotal = existingRecords
            .Where(x => x.PaymentTermsType == selectedPaymentTermsUdf.Id)
            .Sum(x => x.GstAmount);

        if (downPaymentAmount > (purchaseOrder.DocTotal ?? 0))
            return (false, "Down payment amount cannot be more than total PO value", null);

        var remainingGstTotal =
            (((purchaseOrder.VatSum ?? 0) * (selectedPaymentTermsUdf.Gst ?? 0)) / 100)
            - (paidGstTotal ?? 0);

        var remainingBasicTotal =
            ((totalBasic * (selectedPaymentTermsUdf.Basic ?? 0)) / 100)
            - (paidBasicTotal ?? 0);

        if (downPaymentAmount > payableAmount)
            return (false, "Down payment amount cannot exceed the payable amount for the stage.", null);

        if (downPaymentAmount > remainingBasicTotal &&
            (selectedPaymentTermsUdf.Gst == null || selectedPaymentTermsUdf.Gst == 0)
            && purchaseOrder.DocumentStatus != "bost_Close"
            && selectedPaymentTermsUdf.Type is not ("Invoice" or "Retention")
            && selectedPaymentTermsUdf.Basic != null && selectedPaymentTermsUdf.Basic != 0)
        {
            return (false, "Down payment amount cannot exceed remaining basic amount when GST is 0", null);
        }

        if (selectedPaymentTermsUdf.Basic is null or 0
            && selectedPaymentTermsUdf.Gst != null && selectedPaymentTermsUdf.Gst != 0
            && remainingGstTotal < downPaymentAmount
            && purchaseOrder.DocumentStatus != "bost_Close"
            && selectedPaymentTermsUdf.Type is not ("Invoice" or "Retention"))
        {
            return (false, "GST cannot exceed remaining GST amount", null);
        }

        var entity1 = entity;
        entity1.CompanyDb = CompanyDb;
        entity1.PurchaseOrderId ??= await purchaseOrderLinks.EnsureIdFromSapPoAsync(purchaseOrder);
        var termForRemark = StageWisePaymentCalculations.ResolvePaymentTermForRemark(
            purchaseOrder, selectedPaymentTermsUdf);
        var paymentTermsLabel = StageWisePaymentCalculations.FormatDownPaymentRemarkLabel(termForRemark);
        entity1.StageDesc = desc;
        entity1.WtCode = wtCode;
        entity1.PaymentTermsType = selectedPaymentTermsUdf.Id;
        entity1.DocNumber = purchaseOrder.DocNum;
        entity1.Status = StageWisePaymentStatus.Added;

        var gstOnly = StageWisePaymentCalculations.IsGstOnlyTerm(selectedPaymentTermsUdf);
        var skipDownPaymentWt = gstOnly
            || StageWisePaymentCalculations.HasPriorActivePayment(existingRecords)
            || StageWisePaymentCalculations.TdsAlreadyTaken(existingRecords);
        var skipInvoiceWt = gstOnly
            || StageWisePaymentCalculations.InvoiceWithholdingAlreadyTaken(existingRecords, entity1.ApInvoiceDocEntry);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                await PersistPaymentRequestOrAbortAsync(entity1);

                var paymentRequestId = FormatPaymentRequestId(entity1.Id)
                    ?? throw new StageWisePaymentCreateAbortedException("Failed to allocate payment request ID.");

                SapBaseResponse? sapResponse = null;
                double tdsAmount = 0;

                if (purchaseOrder.DocumentStatus == "bost_Close" || selectedPaymentTermsUdf.Type is "Invoice" or "Retention")
                {
                    var (gross, gst) = StageWisePaymentCalculations.SplitAmountForPaymentTerm(
                        purchaseOrder, selectedPaymentTermsUdf, downPaymentAmount, totalBasic, existingRecords);
                    entity1.GrossAmount = gross;
                    entity1.GstAmount = gst;
                    (sapResponse, tdsAmount) = await AddToSap(
                        purchaseOrder, selectedPaymentTermsUdf, false, downPaymentAmount, wtCode, paymentTermsLabel,
                        entity1.Bank, entity1.ApInvoiceDocEntry, skipInvoiceWt, paymentRequestId);
                    if (sapResponse is not null && sapResponse.PendingApproval)
                    {
                        entity1.ApprovalRequestId = sapResponse.PendingApprovalRequestId?.ToString();
                        entity1.Tds = tdsAmount;
                    }
                    else if (sapResponse?.Error?.Message?.Value is not null)
                    {
                        throw new StageWisePaymentCreateAbortedException($"SAP Error: {sapResponse.Error.Message.Value}");
                    }
                    else if (sapResponse?.BaseDocEntry.HasValue == true)
                    {
                        entity1.ApDownPaymentInvoiceEntryNumber = sapResponse.BaseDocNum?.ToString();
                        entity1.Tds = tdsAmount;
                        entity1.ApDownPaymentInvoiceDocEntry = sapResponse.BaseDocEntry?.ToString();
                    }
                }
                else if (selectedPaymentTermsUdf.Basic != null && selectedPaymentTermsUdf.Basic != 0)
                {
                    if (downPaymentAmount > remainingBasicTotal &&
                        selectedPaymentTermsUdf.Gst != null &&
                        selectedPaymentTermsUdf.Gst != 0)
                    {
                        entity1.GrossAmount = remainingBasicTotal;
                        entity1.GstAmount = Math.Round(downPaymentAmount - remainingBasicTotal, 2);
                    }
                    else
                    {
                        entity1.GrossAmount = downPaymentAmount;
                        entity1.GstAmount = 0;
                    }

                    var (dpOk, dpMessage, dpTds, _) = await ApplySeparateDownPaymentsAsync(
                        entity1,
                        purchaseOrder,
                        paymentTermsLabel,
                        wtCode,
                        entity1.GrossAmount ?? 0,
                        entity1.GstAmount ?? 0,
                        skipDownPaymentWt,
                        paymentRequestId);
                    if (!dpOk)
                        throw new StageWisePaymentCreateAbortedException(dpMessage);
                    entity1.Tds = dpTds;
                }
                else if (selectedPaymentTermsUdf.Gst != null && selectedPaymentTermsUdf.Gst != 0)
                {
                    entity1.GstAmount = downPaymentAmount;
                    entity1.GrossAmount = 0;

                    var (dpOk, dpMessage, dpTds, _) = await ApplySeparateDownPaymentsAsync(
                        entity1,
                        purchaseOrder,
                        paymentTermsLabel,
                        wtCode,
                        grossAmount: 0,
                        gstAmount: downPaymentAmount,
                        skipDownPaymentWt,
                        paymentRequestId);
                    if (!dpOk)
                        throw new StageWisePaymentCreateAbortedException(dpMessage);
                    entity1.Tds = dpTds;
                }

                if (string.IsNullOrEmpty(entity1.ApDownPaymentInvoiceEntryNumber)
                    && string.IsNullOrEmpty(entity1.ApprovalRequestId))
                {
                    throw new StageWisePaymentCreateAbortedException("No records saved in SAP!");
                }

                entity1.Status = !string.IsNullOrEmpty(entity1.ApprovalRequestId)
                    ? StageWisePaymentStatus.PendingApproval
                    : StageWisePaymentStatus.Added;
                entity1.LastModifiedOn = DateTime.UtcNow;
                context.StageWisePayments.Update(entity1);
            });
        }
        catch (StageWisePaymentCreateAbortedException ex)
        {
            return (false, ex.Message, null);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(entity1.ApDownPaymentInvoiceEntryNumber)
                || !string.IsNullOrEmpty(entity1.ApprovalRequestId))
            {
                return (false, $"SAP payment succeeded but failed to save locally: {ex.Message}", null);
            }

            return (false, ex.Message, null);
        }

        // Outgoing payment is a follow-on after DP/approval is committed — do not roll back DP on OP failure.
        if (purchaseOrder.DocumentStatus != "bost_Close"
            && selectedPaymentTermsUdf.Type is not ("Invoice" or "Retention")
            && HasCompleteDownPaymentDocs(entity1, entity1.GrossAmount ?? 0, entity1.GstAmount ?? 0))
        {
            var paymentInvoices = BuildDownPaymentInvoices(
                entity1.DownPaymentDocEntry ?? string.Empty,
                entity1.GrossAmount ?? 0,
                entity1.GstAmount ?? 0,
                entity1.Tds ?? 0);
            var netOutgoing = Math.Round(paymentInvoices.Sum(x => x.SumApplied), 2);
            var (outgoingResponse, _) = await AddOutgoingPayment(
                purchaseOrder,
                entity.Bank,
                netOutgoing,
                paymentInvoices);

            if (outgoingResponse?.PendingApproval == true)
            {
                entity1.ApprovalRequestId = AppendApprovalRequestId(
                    entity1.ApprovalRequestId,
                    outgoingResponse.PendingApprovalRequestId?.ToString());
                entity1.Status = StageWisePaymentStatus.PendingApproval;
                try
                {
                    await unitOfWork.ExecuteInTransactionAsync(_ =>
                    {
                        context.StageWisePayments.Update(entity1);
                        return Task.CompletedTask;
                    });
                }
                catch (Exception ex)
                {
                    return (false, $"SAP outgoing payment approval noted but failed to save locally: {ex.Message}", null);
                }
            }
            else if (outgoingResponse?.Error?.Message?.Value is not null)
            {
                return (false, $"SAP Error: {outgoingResponse.Error.Message.Value}", null);
            }
            else if (outgoingResponse?.BaseDocNum is not null)
            {
                ApplyOutgoingPaymentResult(entity1, outgoingResponse);
                try
                {
                    await unitOfWork.ExecuteInTransactionAsync(_ =>
                    {
                        context.StageWisePayments.Update(entity1);
                        return Task.CompletedTask;
                    });
                }
                catch (Exception ex)
                {
                    return (false, $"SAP outgoing payment succeeded but failed to save locally: {ex.Message}", null);
                }
            }
        }

        return (true, "Payment created successfully", entity1.Id);
    }

    public async Task<(bool IsSuccess, string Message, StageWisePayment? Payment)> CreateBatchDownPaymentAsync(
        SapPurchaseOrdersResponse purchaseOrder,
        IReadOnlyList<StageWisePaymentBatchLineRequest> lines,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        double totalBasic,
        string? bank,
        string? wtCode,
        List<StageWisePayment> existingRecords,
        string? userRemark = null,
        DateTime? postingDate = null,
        DateTime? paymentDate = null,
        bool persist = true,
        CancellationToken cancellationToken = default)
    {
        if (purchaseOrder is null)
            return (false, "Purchase order not found!", null);

        if (lines.Count == 0)
            return (false, "No down payment lines provided.", null);

        var totalAmount = Math.Round(lines.Sum(l => l.Amount), 2);
        if (totalAmount <= 0)
            return (false, "Down payment amount cannot be less than or equal to 0!", null);

        if (totalAmount > (purchaseOrder.DocTotal ?? 0))
            return (false, "Down payment amount cannot be more than total PO value", null);

        var totalGross = 0.0;
        var totalGst = 0.0;
        foreach (var line in lines)
        {
            var (gross, gst) = StageWisePaymentCalculations.SplitBatchLineAmount(
                purchaseOrder,
                paymentTerms,
                line.PaymentTermsTypes,
                line.Amount,
                totalBasic,
                existingRecords);
            totalGross += gross;
            totalGst += gst;
        }

        totalGross = Math.Round(totalGross, 2);
        totalGst = Math.Round(totalGst, 2);
        const string batchDesc = "Batch down payment";
        var hadTdsDeducted = StageWisePaymentCalculations.HasPriorActivePayment(existingRecords)
            || StageWisePaymentCalculations.TdsAlreadyTaken(existingRecords);

        var entity = new StageWisePayment
        {
            CompanyDb = CompanyDb,
            DocNumber = purchaseOrder.DocNum,
            PurchaseOrderId = await purchaseOrderLinks.EnsureIdFromSapPoAsync(purchaseOrder),
            Bank = bank,
            WtCode = wtCode,
            GrossAmount = totalGross,
            GstAmount = totalGst,
            StageDesc = batchDesc,
            Stage = StageWisePaymentStages.AfterReceiptOfMaterial,
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = DateTime.UtcNow,
        };

        string? outgoingError = null;
        var nestInAmbient = !persist && unitOfWork.HasActiveTransaction;
        try
        {
            // When called under an ambient batch transaction (persist:false), nest and leave commit to caller.
            // Otherwise own a transaction: insert → SAP/approval → update → commit (or rollback on SAP fail).
            if (nestInAmbient)
            {
                outgoingError = await ExecuteBatchDownPaymentCoreAsync(
                    entity, purchaseOrder, lines, paymentTerms, existingRecords, totalBasic, batchDesc, wtCode, bank,
                    totalGross, totalGst, hadTdsDeducted, userRemark, postingDate, paymentDate, cancellationToken);
            }
            else
            {
                await unitOfWork.ExecuteInTransactionAsync(
                    async ct =>
                    {
                        outgoingError = await ExecuteBatchDownPaymentCoreAsync(
                            entity, purchaseOrder, lines, paymentTerms, existingRecords, totalBasic, batchDesc, wtCode, bank,
                            totalGross, totalGst, hadTdsDeducted, userRemark, postingDate, paymentDate, ct);
                    },
                    cancellationToken);
            }
        }
        catch (StageWisePaymentCreateAbortedException) when (nestInAmbient)
        {
            // Let the outer batch transaction roll back payment/approval drafts.
            throw;
        }
        catch (StageWisePaymentCreateAbortedException ex)
        {
            return (false, ex.Message, null);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(entity.ApDownPaymentInvoiceEntryNumber)
                || !string.IsNullOrEmpty(entity.ApprovalRequestId))
            {
                return (false, $"SAP payment succeeded but failed to save locally: {ex.Message}", null);
            }

            return (false, ex.Message, null);
        }

        if (!string.IsNullOrEmpty(outgoingError))
            return (false, outgoingError, entity);

        return (true, persist ? "Payment created successfully" : "Payment prepared successfully", entity);
    }

    /// <returns>Outgoing-payment error after DP succeeded (caller should keep the DP row); null on full success.</returns>
    private async Task<string?> ExecuteBatchDownPaymentCoreAsync(
        StageWisePayment entity,
        SapPurchaseOrdersResponse purchaseOrder,
        IReadOnlyList<StageWisePaymentBatchLineRequest> lines,
        IReadOnlyList<PaymentTermsUdf> paymentTerms,
        List<StageWisePayment> existingRecords,
        double totalBasic,
        string batchDesc,
        string? wtCode,
        string? bank,
        double totalGross,
        double totalGst,
        bool hadTdsDeducted,
        string? userRemark,
        DateTime? postingDate,
        DateTime? paymentDate,
        CancellationToken cancellationToken)
    {
        await PersistPaymentRequestOrAbortAsync(entity, cancellationToken);

        var paymentRequestId = FormatPaymentRequestId(entity.Id)
            ?? throw new StageWisePaymentCreateAbortedException("Failed to allocate payment request ID.");

        double tdsAmount = 0;
        var tdsTaken = hadTdsDeducted;
        var expectedDpCount = 0;
        var postedDps = new List<PostedDownPayment>();

        foreach (var line in lines)
        {
            var (gross, gst) = StageWisePaymentCalculations.SplitBatchLineAmount(
                purchaseOrder,
                paymentTerms,
                line.PaymentTermsTypes,
                line.Amount,
                totalBasic,
                existingRecords);

            var term = StageWisePaymentCalculations.ResolveDownPaymentTerm(paymentTerms, line.PaymentTermsTypes);
            var label = StageWisePaymentCalculations.FormatDownPaymentRemarkLabel(term);
            var skipTds = tdsTaken || StageWisePaymentCalculations.IsGstOnlyTerm(term);
            if (gross > 0) expectedDpCount++;
            if (gst > 0) expectedDpCount++;

            var (dpOk, dpMessage, dpTds, posted) = await ApplySeparateDownPaymentsAsync(
                entity,
                purchaseOrder,
                string.IsNullOrWhiteSpace(label) ? batchDesc : label,
                wtCode,
                gross,
                gst,
                skipTds,
                paymentRequestId,
                postingDate,
                paymentDate,
                userRemark,
                append: true);
            if (!dpOk)
                throw new StageWisePaymentCreateAbortedException(dpMessage);
            tdsAmount += dpTds;
            postedDps.AddRange(posted);
            if (dpTds > 0)
                tdsTaken = true;
        }

        entity.Tds = Math.Round(tdsAmount, 2);
        if (!string.IsNullOrEmpty(entity.ApprovalRequestId))
            entity.Status = StageWisePaymentStatus.PendingApproval;
        else if (!string.IsNullOrWhiteSpace(entity.ApDownPaymentInvoiceEntryNumber))
            entity.Status = StageWisePaymentStatus.Added;
        else
            throw new StageWisePaymentCreateAbortedException("No records saved in SAP!");

        string? outgoingError = null;
        if (purchaseOrder.DocumentStatus != "bost_Close"
            && HasCompleteDownPaymentDocs(entity, expectedDpCount))
        {
            var paymentInvoices = BuildDownPaymentInvoices(postedDps, entity.Tds ?? 0);
            var netOutgoing = Math.Round(paymentInvoices.Sum(x => x.SumApplied), 2);
            var (outgoingResponse, _) = await AddOutgoingPayment(
                purchaseOrder,
                bank,
                netOutgoing,
                paymentInvoices,
                userRemark,
                paymentDate,
                postingDate);

            if (outgoingResponse?.PendingApproval == true)
            {
                entity.ApprovalRequestId = AppendApprovalRequestId(
                    entity.ApprovalRequestId,
                    outgoingResponse.PendingApprovalRequestId?.ToString());
                entity.Status = StageWisePaymentStatus.PendingApproval;
            }
            else if (outgoingResponse?.Error?.Message?.Value is not null)
            {
                outgoingError = $"SAP Error: {outgoingResponse.Error.Message.Value}";
            }
            else if (outgoingResponse?.BaseDocNum is not null)
            {
                ApplyOutgoingPaymentResult(entity, outgoingResponse);
            }
        }

        entity.LastModifiedOn = DateTime.UtcNow;
        context.StageWisePayments.Update(entity);
        return outgoingError;
    }

    private async Task<(bool Ok, string Message, double TdsAmount, List<PostedDownPayment> Posted)> ApplySeparateDownPaymentsAsync(
        StageWisePayment entity,
        SapPurchaseOrdersResponse purchaseOrder,
        string? desc,
        string? wtCode,
        double grossAmount,
        double gstAmount,
        bool hadTdsDeducted,
        string? paymentRequestId = null,
        DateTime? postingDate = null,
        DateTime? paymentDate = null,
        string? userRemark = null,
        bool append = false)
    {
        var docNums = new List<string>();
        var docEntries = new List<string>();
        var approvalIds = new List<string>();
        var posted = new List<PostedDownPayment>();
        double tdsAmount = 0;

        if (grossAmount > 0)
        {
            var (sapResponse, basicTds) = await AddDownPayment(
                purchaseOrder, isGst: false, grossAmount, wtCode, desc, hadTdsDeducted,
                paymentRequestId, postingDate, paymentDate, userRemark);

            if (sapResponse?.PendingApproval == true)
            {
                if (sapResponse.PendingApprovalRequestId.HasValue)
                    approvalIds.Add(sapResponse.PendingApprovalRequestId.Value.ToString());
                tdsAmount += basicTds;
            }
            else if (sapResponse?.Error?.Message?.Value is not null)
            {
                return (false, $"SAP Error: {sapResponse.Error.Message.Value}", 0, posted);
            }
            else if (sapResponse?.BaseDocEntry.HasValue == true)
            {
                if (sapResponse.BaseDocNum.HasValue)
                    docNums.Add(sapResponse.BaseDocNum.Value.ToString());
                docEntries.Add(sapResponse.BaseDocEntry.Value.ToString());
                posted.Add(new PostedDownPayment(sapResponse.BaseDocEntry.Value, grossAmount, IsGst: false));
                tdsAmount += basicTds;
            }
            else
            {
                return (false, "No Basic down payment was created in SAP.", 0, posted);
            }
        }

        if (gstAmount > 0)
        {
            var (sapResponse, _) = await AddDownPayment(
                purchaseOrder, isGst: true, gstAmount, wtCode, desc, hadTdsDeducted,
                paymentRequestId, postingDate, paymentDate, userRemark);

            if (sapResponse?.PendingApproval == true)
            {
                if (sapResponse.PendingApprovalRequestId.HasValue)
                    approvalIds.Add(sapResponse.PendingApprovalRequestId.Value.ToString());
            }
            else if (sapResponse?.Error?.Message?.Value is not null)
            {
                return (false, $"SAP Error: {sapResponse.Error.Message.Value}", 0, posted);
            }
            else if (sapResponse?.BaseDocEntry.HasValue == true)
            {
                if (sapResponse.BaseDocNum.HasValue)
                    docNums.Add(sapResponse.BaseDocNum.Value.ToString());
                docEntries.Add(sapResponse.BaseDocEntry.Value.ToString());
                posted.Add(new PostedDownPayment(sapResponse.BaseDocEntry.Value, gstAmount, IsGst: true));
            }
            else
            {
                return (false, "No GST down payment was created in SAP.", 0, posted);
            }
        }

        if (docNums.Count > 0)
            entity.ApDownPaymentInvoiceEntryNumber = append && !string.IsNullOrWhiteSpace(entity.ApDownPaymentInvoiceEntryNumber)
                ? entity.ApDownPaymentInvoiceEntryNumber + "," + string.Join(',', docNums)
                : string.Join(',', docNums);
        if (docEntries.Count > 0)
        {
            var joined = string.Join(',', docEntries);
            entity.DownPaymentDocEntry = append && !string.IsNullOrWhiteSpace(entity.DownPaymentDocEntry)
                ? entity.DownPaymentDocEntry + "," + joined
                : joined;
            entity.ApDownPaymentInvoiceDocEntry = entity.DownPaymentDocEntry;
        }
        if (approvalIds.Count > 0)
            entity.ApprovalRequestId = append
                ? AppendApprovalRequestId(entity.ApprovalRequestId, string.Join(',', approvalIds))
                : string.Join(',', approvalIds);

        return (true, string.Empty, Math.Round(tdsAmount, 2), posted);
    }

    private static bool HasCompleteDownPaymentDocs(StageWisePayment entity, int expectedCount)
    {
        if (expectedCount <= 0)
            return false;

        var count = entity.DownPaymentDocEntry?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length ?? 0;
        return count == expectedCount;
    }

    private static bool HasCompleteDownPaymentDocs(StageWisePayment entity, double grossAmount, double gstAmount) =>
        HasCompleteDownPaymentDocs(entity, (grossAmount > 0 ? 1 : 0) + (gstAmount > 0 ? 1 : 0));

    private static List<PaymentInvoice> BuildDownPaymentInvoices(
        string downPaymentDocEntries,
        double grossAmount,
        double gstAmount,
        double tdsAmount)
    {
        var entries = downPaymentDocEntries
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : (int?)null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        var invoices = new List<PaymentInvoice>();
        var lineNumber = 0;
        var entryIndex = 0;

        if (grossAmount > 0 && entryIndex < entries.Count)
        {
            invoices.Add(new PaymentInvoice
            {
                LineNumber = lineNumber++,
                DocEntry = entries[entryIndex++],
                InvoiceType = Constants.SapVendorPaymentInvoiceType.DownPayment,
                AppliedFC = 0,
                SumApplied = Math.Round(Math.Max(0, grossAmount - tdsAmount), 2),
            });
        }

        if (gstAmount > 0 && entryIndex < entries.Count)
        {
            invoices.Add(new PaymentInvoice
            {
                LineNumber = lineNumber++,
                DocEntry = entries[entryIndex],
                InvoiceType = Constants.SapVendorPaymentInvoiceType.DownPayment,
                AppliedFC = 0,
                SumApplied = Math.Round(gstAmount, 2),
            });
        }

        return invoices;
    }

    private static List<PaymentInvoice> BuildDownPaymentInvoices(
        IReadOnlyList<PostedDownPayment> posted,
        double tdsAmount)
    {
        var remainingTds = tdsAmount;
        var invoices = new List<PaymentInvoice>();
        var lineNumber = 0;
        foreach (var dp in posted)
        {
            var applied = dp.Amount;
            if (!dp.IsGst && remainingTds > 0)
            {
                var take = Math.Min(remainingTds, applied);
                applied = Math.Round(applied - take, 2);
                remainingTds = Math.Round(remainingTds - take, 2);
            }

            if (applied <= 0)
                continue;

            invoices.Add(new PaymentInvoice
            {
                LineNumber = lineNumber++,
                DocEntry = dp.DocEntry,
                InvoiceType = Constants.SapVendorPaymentInvoiceType.DownPayment,
                AppliedFC = 0,
                SumApplied = applied,
            });
        }

        return invoices;
    }

    private static void ApplyOutgoingPaymentResult(StageWisePayment entity, SapBaseResponse outgoingResponse)
    {
        entity.PaymentDocEntry = outgoingResponse.BaseDocEntry?.ToString();
        if (string.IsNullOrEmpty(entity.ApDownPaymentInvoiceEntryNumber))
            entity.ApDownPaymentInvoiceEntryNumber = outgoingResponse.BaseDocNum?.ToString();
        else
            entity.ApDownPaymentInvoiceEntryNumber += "," + outgoingResponse.BaseDocNum;
    }

    private static string? AppendApprovalRequestId(string? existing, string? next)
    {
        if (string.IsNullOrWhiteSpace(next))
            return existing;
        if (string.IsNullOrWhiteSpace(existing))
            return next;
        return existing + "," + next;
    }

    /// <summary>Formats <see cref="StageWisePayment.Id"/> for SAP UDF U_BSC_3.</summary>
    public static string? FormatPaymentRequestId(int? id) =>
        id is null or <= 0 ? null : id.Value.ToString();

    /// <summary>
    /// Inserts the payment request to allocate an identity Id. When an ambient transaction is open,
    /// the insert is not committed until the outer transaction commits (rollback removes orphans).
    /// </summary>
    public async Task<(bool Success, string Message)> EnsurePaymentRequestPersistedAsync(
        StageWisePayment entity,
        CancellationToken cancellationToken = default)
    {
        if (entity.Id > 0)
            return (true, string.Empty);

        entity.CompanyDb = string.IsNullOrWhiteSpace(entity.CompanyDb) ? CompanyDb : entity.CompanyDb;
        entity.CreatedOn = DateTime.UtcNow;
        entity.LastModifiedOn = DateTime.UtcNow;
        if (entity.Status == default)
            entity.Status = StageWisePaymentStatus.Added;

        try
        {
            if (unitOfWork.HasActiveTransaction)
            {
                await context.StageWisePayments.AddAsync(entity, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else
            {
                await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    await context.StageWisePayments.AddAsync(entity, ct);
                }, cancellationToken);
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to allocate payment request ID: {ex.Message}");
        }
    }

    private async Task PersistPaymentRequestOrAbortAsync(
        StageWisePayment entity,
        CancellationToken cancellationToken = default)
    {
        var (persisted, persistMessage) = await EnsurePaymentRequestPersistedAsync(entity, cancellationToken);
        if (!persisted)
            throw new StageWisePaymentCreateAbortedException(persistMessage);
    }

    public Task DiscardDraftPaymentRequestAsync(int paymentRequestId, CancellationToken cancellationToken = default) =>
        RemoveDraftPaymentRequestAsync(paymentRequestId, cancellationToken);

    private async Task RemoveDraftPaymentRequestAsync(int paymentRequestId, CancellationToken cancellationToken = default)
    {
        // Prefer rolling back an ambient create transaction instead of soft-deleting drafts.
        if (unitOfWork.HasActiveTransaction)
            return;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var draft = await context.StageWisePayments
                    .FirstOrDefaultAsync(x => x.Id == paymentRequestId && x.CompanyDb == CompanyDb, ct);
                if (draft is not null)
                    context.StageWisePayments.Remove(draft);
            }, cancellationToken);
        }
        catch
        {
            // Best-effort cleanup when SAP posting fails after a committed pre-allocation.
        }
    }

    private async Task<(SapBaseResponse? response, double tdsAmount)> AddToSap(
        SapPurchaseOrdersResponse purchaseOrder,
        PaymentTermsUdf paymentTerms,
        bool isGst,
        double amount,
        string? wtCode,
        string? desc, string? bank, string? apInvoiceDoc, bool hadTdsDeducted,
        string? paymentRequestId)
    {
        if (purchaseOrder.DocumentStatus == "bost_Close" || paymentTerms?.Type is "Invoice" or "Retention")
            return await AddOutgoingPayment(
                purchaseOrder, bank, amount, apInvoiceDoc, hadTdsDeducted);
        return await AddDownPayment(
            purchaseOrder, isGst, amount, wtCode, desc, hadTdsDeducted,
            paymentRequestId: paymentRequestId);
    }

    private async Task<(SapBaseResponse? response, double tds)> AddOutgoingPayment(
        SapPurchaseOrdersResponse purchaseOrder,
        string? bank,
        double amount,
        string? apInvoiceDoc,
        bool hadTdsDeducted,
        string? invoiceType = Constants.SapVendorPaymentInvoiceType.Invoice,
        string? userRemark = null)
    {
        SapPurchaseInvoicesResponse? apInvoice = null;
        if (int.TryParse(apInvoiceDoc, out var apInvoiceDocEntry))
        {
            apInvoice = await sapVendorPaymentService.GetApInvoiceByDocEntry(
                purchaseOrder?.CardCode ?? string.Empty,
                apInvoiceDocEntry);
        }

        if (apInvoice is null && purchaseOrder?.DocEntry is int poDocEntry)
        {
            var apInvoices = await sapVendorPaymentService.GetApInvoicesForPurchaseOrder(
                purchaseOrder.CardCode ?? string.Empty,
                poDocEntry);
            apInvoice = apInvoices?.Value?.FirstOrDefault(x => x.DocEntry.ToString() == apInvoiceDoc);
        }

        if (invoiceType == Constants.SapVendorPaymentInvoiceType.Invoice && (apInvoice is null || apInvoice.DocEntry is null))
        {
            return (new SapBaseResponse
            {
                Error = new SapError
                {
                    Code = -1,
                    Message = new SapMessage
                    {
                        Value = "No AP Invoice found for the purchase order. Cannot create payment."
                    }
                }
            }, 0);
        }

        var net = amount - (hadTdsDeducted ? 0 : apInvoice?.WTAmount ?? 0);
        net = StageWisePaymentCalculations.CapAppliedAmountToSapOpen(net, apInvoice);
        if (net <= 0)
        {
            return (
               new SapBaseResponse
               {
                   Error = new SapError
                   {
                       Code = -1,
                       Message = new SapMessage
                       {
                           Value = "Net payment amount cannot be less than or equal to 0. Payment not created."
                       }
                   }
               }, 0);
        }

        var invoices = new List<PaymentInvoice>
        {
            new()
            {
                DocEntry = apInvoice?.DocEntry ?? int.Parse(apInvoiceDoc ?? "0"),
                InvoiceType = invoiceType,
                AppliedFC = 0,
                LineNumber = 0,
                SumApplied = net,
            },
        };

        var (response, _) = await AddOutgoingPayment(purchaseOrder, bank, net, invoices, userRemark);
        if (response is not null)
            response.SupportingData = (apInvoice?.WTAmount ?? 0).ToString();
        return (response, hadTdsDeducted ? 0 : apInvoice?.WTAmount ?? 0);
    }

    private async Task<(SapBaseResponse? response, double tds)> AddOutgoingPayment(
        SapPurchaseOrdersResponse purchaseOrder,
        string? bank,
        double transferSum,
        IReadOnlyList<PaymentInvoice> paymentInvoices,
        string? userRemark = null,
        DateTime? paymentDate = null,
        DateTime? postingDate = null)
    {
        if (paymentInvoices.Count == 0 || transferSum <= 0)
        {
            return (
               new SapBaseResponse
               {
                   Error = new SapError
                   {
                       Code = -1,
                       Message = new SapMessage
                       {
                           Value = "Net payment amount cannot be less than or equal to 0. Payment not created."
                       }
                   }
               }, 0);
        }

        var remarks = Constants.PaymentRemarks.Build(
            userRemark, purchaseOrder?.BPLId, purchaseOrder?.DocNum?.ToString());
        var vendorRequest = new SapVendorPaymentRequests
        {
            CardCode = purchaseOrder?.CardCode ?? "",
            TransferAccount = bank ?? "_SYS00000000980",
            TransferSum = transferSum.ToString("F2"),
            ProjectCode = purchaseOrder?.Project,
            PoNumber = purchaseOrder?.DocNum?.ToString() ?? "",
            Remarks = remarks,
            JournalRemarks = remarks,
            PaymentInvoices = paymentInvoices.ToList(),
            BPLId = purchaseOrder?.BPLId ?? 1,
        };
        // Prefer payment date for DocDate; fall back to today when omitted (single-payment create).
        ApplyVendorPaymentDates(vendorRequest, paymentDate ?? DateTime.UtcNow, postingDate ?? paymentDate);

        SapVendorPaymentsResponse? sapResponse;
        try
        {
            sapResponse = await sapVendorPaymentService.CreateVendorPayments(
                vendorRequest, supportingData: purchaseOrder?.DocEntry.ToString());
        }
        catch (ApiErrorException ex)
        {
            return (new SapBaseResponse
            {
                Error = new SapError { Message = new SapMessage { Value = ex.Message } },
            }, 0);
        }

        if (sapResponse is not null)
        {
            sapResponse.BaseDocEntry = sapResponse.DocEntry;
            sapResponse.BaseDocNum = sapResponse.DocNumber;
        }

        return (sapResponse, 0);
    }

    private async Task<(SapBaseResponse? response, double tdsAmount)> AddDownPayment(
        SapPurchaseOrdersResponse purchaseOrder,
        bool isGst,
        double amount,
        string? wtCode,
        string? desc,
        bool hadTdsDeducted,
        string? paymentRequestId = null,
        DateTime? postingDate = null,
        DateTime? paymentDate = null,
        string? userRemark = null)
    {
        // Draw PO lines with LineTotals that sum to the requested amount. Sending base refs alone
        // makes SAP copy the full open PO line value, which triggers
        // "Total Down Payment Requests exceed the Purchase Order value" (20026) even when
        // DownPaymentAmount is only a portion of the PO.
        // Never send Service Layer "DownPayment" — that is DpmPrcnt (%). Use DownPaymentAmount (DpmAmnt).
        var sourceLines = purchaseOrder.DocumentLines ?? [];
        if (sourceLines.Count == 0)
        {
            return (new SapBaseResponse
            {
                Error = new SapError
                {
                    Message = new SapMessage
                    {
                        Value = "Purchase order lines are not available locally. Sync this purchase order from SAP, then retry.",
                    },
                },
            }, 0);
        }

        var roundedAmount = Math.Round(amount, 2);
        if (roundedAmount <= 0)
        {
            return (new SapBaseResponse
            {
                Error = new SapError
                {
                    Message = new SapMessage { Value = "Down payment amount must be greater than zero." },
                },
            }, 0);
        }

        var documentLines = BuildDownPaymentDocumentLines(
            purchaseOrder,
            sourceLines,
            roundedAmount,
            isGst,
            hadTdsDeducted);

        if (documentLines.Count == 0)
        {
            return (new SapBaseResponse
            {
                Error = new SapError
                {
                    Message = new SapMessage
                    {
                        Value = "Purchase order has no line amounts to allocate for the down payment.",
                    },
                },
            }, 0);
        }

        var comments = Constants.PaymentRemarks.BuildDownPayment(
            desc, purchaseOrder.BPLId, purchaseOrder.DocNum?.ToString());
        var req = new SapPurchaseDownPaymentRequest
        {
            DocumentLines = documentLines,
            CardCode = purchaseOrder.CardCode,
            // Amount only — omit DownPayment (%) so SAP does not treat the rupee value as a percent.
            DownPayment = null,
            DownPaymentAmount = roundedAmount,
            DocType = purchaseOrder.DocType,
            // Must match sum of LineTotals so SAP does not expand to full PO open value.
            DocTotal = roundedAmount,
            BPLId = purchaseOrder.BPLId ?? 1,
            Comments = comments,
            // JrnlMemo: user remarks when provided; otherwise same text as Comments (SapForms pattern).
            JournalMemo = string.IsNullOrWhiteSpace(userRemark) ? comments : userRemark.Trim(),
            PaymentRequestId = paymentRequestId,
            WithholdingTaxDataCollection = null,
        };
        ApplyPostingDate(req, postingDate, paymentDate);

        if (!isGst && !hadTdsDeducted && !string.IsNullOrWhiteSpace(wtCode))
        {
            req.WithholdingTaxDataCollection =
            [
                new SapWithholdingTaxDataCollectionResponse
                {
                    WtCode = wtCode,
                },
            ];
        }

        SapPurchaseDownPaymentResponse? sapResponse;
        try
        {
            sapResponse = await sapPurchaseDownPaymentService.SaveDownPayment(
                req, supportingData: purchaseOrder.DocEntry?.ToString());
        }
        catch (ApiErrorException ex)
        {
            return (new SapBaseResponse
            {
                Error = new SapError { Message = new SapMessage { Value = ex.Message } },
            }, 0);
        }
        double tdsAmount = 0;
        if (sapResponse is not null)
        {
            sapResponse.BaseDocEntry = sapResponse.DocEntry;
            sapResponse.BaseDocNum = sapResponse.DocNum;
            tdsAmount = hadTdsDeducted ? 0 : sapResponse.WTAmount ?? 0;
        }
        return (sapResponse, tdsAmount);
    }

    /// <summary>
    /// Maps batch dates onto SAP PurchaseDownPayments fields.
    /// Document date (<c>DocDate</c>) prefers payment date when provided; tax/posting date
    /// (<c>TaxDate</c>) prefers posting date — matching VendorPayments DocDate/TaxDate split.
    /// </summary>
    public static void ApplyPostingDate(
        SapPurchaseDownPaymentRequest request,
        DateTime? postingDate,
        DateTime? paymentDate = null)
    {
        var docDate = (paymentDate ?? postingDate)?.Date;
        var taxDate = (postingDate ?? paymentDate)?.Date;
        if (docDate is null)
            return;

        request.DocDate = docDate;
        request.TaxDate = taxDate;
    }

    /// <summary>
    /// Maps payment/posting dates onto VendorPayments DocDate (document date) and TaxDate.
    /// </summary>
    public static void ApplyVendorPaymentDates(
        SapVendorPaymentRequests request,
        DateTime? paymentDate,
        DateTime? postingDate = null)
    {
        var pay = (paymentDate ?? postingDate)?.Date;
        if (pay is null)
            return;

        var post = (postingDate ?? paymentDate)!.Value.Date;
        request.TransferDate = pay.Value;
        request.DocDueDate = pay.Value;
        request.DocDate = pay.Value;
        request.PostingDate = post;
    }

    /// <summary>
    /// Builds ODPO lines linked to the PO with LineTotals that sum exactly to <paramref name="amount"/>.
    /// </summary>
    public static List<SapInventoryTransferItemsRequests> BuildDownPaymentDocumentLines(
        SapPurchaseOrdersResponse purchaseOrder,
        IReadOnlyList<SapInventoryTransferItemsRequests> sourceLines,
        double amount,
        bool isGst,
        bool hadTdsDeducted = false)
    {
        var weighted = sourceLines
            .Select(line => (Line: line, Weight: GetDownPaymentLineWeight(line)))
            .Where(x => x.Weight > 0)
            .ToList();

        if (weighted.Count == 0)
            return [];

        var totalWeight = weighted.Sum(x => x.Weight);
        var allocated = 0.0;
        var result = new List<SapInventoryTransferItemsRequests>(weighted.Count);
        var wtLiable = isGst || hadTdsDeducted
            ? Constants.SapBoolean.SapFalse
            : Constants.SapBoolean.SapTrue;

        for (var i = 0; i < weighted.Count; i++)
        {
            var (line, weight) = weighted[i];
            var lineTotal = i == weighted.Count - 1
                ? Math.Round(amount - allocated, 2)
                : Math.Round(amount * weight / totalWeight, 2);
            allocated += lineTotal;

            if (lineTotal <= 0)
                continue;

            double? quantity = null;
            double? unitPrice = line.UnitPrice;
            if (line.Quantity is > 0 && weight > 0)
            {
                quantity = Math.Round(line.Quantity.Value * lineTotal / weight, 6);
                if (unitPrice is null or <= 0 && quantity > 0)
                    unitPrice = Math.Round(lineTotal / quantity.Value, 6);
            }

            result.Add(new SapInventoryTransferItemsRequests
            {
                ItemCode = line.ItemCode,
                BaseType = 22,
                BaseEntry = purchaseOrder.DocEntry,
                BaseLine = line.LineNum,
                Quantity = quantity,
                UnitPrice = unitPrice,
                DiscountPercent = 0,
                LineTotal = lineTotal,
                WTLiable = wtLiable,
                TaxLiable = Constants.SapBoolean.SapFalse,
                WarehouseCode = line.WarehouseCode,
                ProjectCode = line.ProjectCode ?? purchaseOrder.Project,
            });
        }

        return result;
    }

    private static double GetDownPaymentLineWeight(SapInventoryTransferItemsRequests line)
    {
        if (line.LineTotal is > 0)
            return line.LineTotal.Value;
        if (line.GrossTotal is > 0)
            return line.GrossTotal.Value;

        var afterDisc = line.RowTotalAfterDisc;
        return afterDisc > 0 ? afterDisc : 0;
    }

    public async Task MarkRejectedWhenRequestRejectedAsync(int approvalRequestId)
    {
        var approvalRequestIdStr = approvalRequestId.ToString();
        var records = await context.StageWisePayments
            .Where(x => x.CompanyDb == CompanyDb && x.ApprovalRequestId != null && x.Status == StageWisePaymentStatus.PendingApproval)
            .ToListAsync();

        foreach (var record in records)
        {
            if (!IsLinkedToApprovalRequest(record, approvalRequestIdStr))
                continue;

            record.Status = StageWisePaymentStatus.Cancelled;
            record.LastModifiedOn = DateTime.UtcNow;
            context.AttachModified(record);
            await SyncBatchStatusForPaymentAsync(record.Id, StageWisePaymentBatchStatus.Rejected);
        }

        await unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask);
    }

    public async Task MarkApprovedWhenAllRequestsCompleteAsync(int approvalRequestId, string? companyDb = null)
    {
        var company = companyDb ?? CompanyDb;
        var approvalRequestIdStr = approvalRequestId.ToString();
        var records = await context.StageWisePayments
            .Where(x => x.CompanyDb == company && x.ApprovalRequestId != null && x.Status == StageWisePaymentStatus.PendingApproval)
            .ToListAsync();

        foreach (var record in records)
        {
            if (!IsLinkedToApprovalRequest(record, approvalRequestIdStr))
                continue;

            var requestIds = ParseApprovalRequestIds(record.ApprovalRequestId);
            if (requestIds.Count == 0)
                continue;

            var approvalRows = await context.ApprovalRequests
                .Where(r => r.CompanyDb == company && requestIds.Contains(r.Id))
                .Select(r => new { r.Id, r.OverallStatus, r.DocumentType })
                .ToListAsync();

            if (approvalRows.Count != requestIds.Count)
                continue;

            if (!approvalRows.All(r => r.OverallStatus == ApprovalStatus.Approved))
                continue;

            if (!HasSapDocumentsForLinkedApprovals(record, approvalRows.Select(r => (r.Id, r.DocumentType)).ToList()))
                continue;

            record.Status = StageWisePaymentStatus.Approved;
            record.LastModifiedOn = DateTime.UtcNow;
            context.AttachModified(record);
            await SyncBatchStatusForPaymentAsync(record.Id, StageWisePaymentBatchStatus.Approved, company);
        }

        await unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask);
    }

    /// <summary>
    /// True when every linked approval request has a corresponding SAP document on the payment row.
    /// Outgoing payment approvals require <see cref="StageWisePayment.PaymentDocEntry"/>; down-payment
    /// approvals require down-payment doc entries. This prevents workflow-only approval from clearing
    /// PendingApproval when SAP posting never ran or Finalize aborted mid-request.
    /// </summary>
    static bool HasSapDocumentsForLinkedApprovals(
        StageWisePayment record,
        IReadOnlyList<(int Id, ApprovalDocumentType DocumentType)> approvalRows)
    {
        var approvalIds = approvalRows.Select(r => r.Id.ToString()).ToHashSet(StringComparer.Ordinal);
        var linkedIds = ParseApprovalRequestIds(record.ApprovalRequestId)
            .Select(id => id.ToString())
            .Where(approvalIds.Contains)
            .ToList();

        foreach (var idText in linkedIds)
        {
            var row = approvalRows.First(r => r.Id.ToString() == idText);
            if (row.DocumentType == ApprovalDocumentType.Payments)
            {
                if (string.IsNullOrWhiteSpace(record.PaymentDocEntry))
                    return false;
            }
            else if (row.DocumentType == ApprovalDocumentType.StagewisePayments_DP)
            {
                if (string.IsNullOrWhiteSpace(record.DownPaymentDocEntry)
                    && string.IsNullOrWhiteSpace(record.ApDownPaymentInvoiceEntryNumber))
                    return false;
            }
        }

        return linkedIds.Count > 0;
    }

    private async Task SyncBatchStatusForPaymentAsync(
        int stageWisePaymentId,
        StageWisePaymentBatchStatus status,
        string? companyDb = null)
    {
        var company = companyDb ?? CompanyDb;
        var batch = await context.StageWisePaymentBatches
            .FirstOrDefaultAsync(b => b.CompanyDb == company
                && (b.StageWisePaymentId == stageWisePaymentId
                    || b.DownPaymentStageWisePaymentId == stageWisePaymentId));
        if (batch is null)
            return;

        batch.Status = status;
        batch.LastModifiedOn = DateTime.UtcNow;
        context.AttachModified(batch);
    }

    static bool IsLinkedToApprovalRequest(StageWisePayment record, string approvalRequestId) =>
        record.ApprovalRequestId?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(x => x == approvalRequestId) == true;

    static List<int> ParseApprovalRequestIds(string? approvalRequestIds) =>
        approvalRequestIds?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => int.TryParse(id, out var parsed) ? parsed : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList() ?? [];

    public async Task<(bool Success, string Message)> DeleteStageWisePayment(StageWisePayment record)
    {

        var docEntries = record.ApDownPaymentInvoiceEntryNumber?.Split(',').Select(x => x.Trim())
            .Where(x => !string.IsNullOrEmpty(x)).ToList();
        if (docEntries is not null && docEntries.Count > 0)
            return (false, "Cant delete record with existing SAP entries. Please contact admin.");

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                context.StageWisePayments.Remove(record);

                var recordApprovalRequests = record.ApprovalRequestId?.Split(",").ToList() ?? [];
                var approvalRequests = context.ApprovalRequests.Where(x => x.CompanyDb == CompanyDb && record.ApprovalRequestId != null
                    && recordApprovalRequests.Contains(x.Id.ToString())).ToList();
                context.ApprovalRequests.RemoveRange(approvalRequests);
                await Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            return (false, $"Failed to delete payment record: {ex.Message}");
        }

        return (true, "Stage wise payment record deleted successfully.");
    }

    public async Task<(bool Success, IReadOnlyList<(bool Success, string Message)> Operations)> CancelOutgoingPayment(
        StageWisePayment record,
        bool syncBatchStatus = true)
    {
        var operations = new List<(bool Success, string Message)>();
        var existingRecord = await context.StageWisePayments.FindAsync(record.Id);
        if (existingRecord is null)
        {
            operations.Add((false, "Record not found."));
            return (false, operations);
        }

        var linkedDocNums = SplitLinkedDocs(existingRecord.ApDownPaymentInvoiceEntryNumber);
        var hasSapDocs = linkedDocNums.Count > 0
            || SplitLinkedDocs(existingRecord.DownPaymentDocEntry).Count > 0
            || SplitLinkedDocs(existingRecord.PaymentDocEntry).Count > 0
            || SplitLinkedDocs(existingRecord.ApDownPaymentInvoiceDocEntry).Count > 0;

        if (!hasSapDocs)
        {
            if (existingRecord.Status == StageWisePaymentStatus.PendingApproval
                || existingRecord.Status == StageWisePaymentStatus.Added)
            {
                try
                {
                    await unitOfWork.ExecuteInTransactionAsync(async _ =>
                    {
                        existingRecord.GrossAmount = 0;
                        existingRecord.GstAmount = 0;
                        existingRecord.Tds = 0;
                        existingRecord.Status = StageWisePaymentStatus.Cancelled;
                        existingRecord.LastModifiedOn = DateTime.UtcNow;
                        context.StageWisePayments.Update(existingRecord);
                        if (syncBatchStatus)
                            await SyncBatchStatusForPaymentAsync(existingRecord.Id, StageWisePaymentBatchStatus.Cancelled);
                    });
                }
                catch (Exception ex)
                {
                    operations.Add((false, $"Failed to update cancellation status: {ex.Message}"));
                    return (false, operations);
                }

                operations.Add((true, "Payment marked as cancelled (no SAP documents to cancel)."));
                return (true, operations);
            }

            operations.Add((false, "No SAP documents linked to this record. Cannot cancel in SAP."));
            return (false, operations);
        }

        var allCancelledInSap = true;

        foreach (var entry in SplitLinkedDocs(existingRecord.PaymentDocEntry))
        {
            var vp = await TryCancelVendorPaymentAsync(entry, operations);
            if (vp == CancelAttempt.Failed)
                allCancelledInSap = false;
        }

        var downPaymentKeys = SplitLinkedDocs(existingRecord.DownPaymentDocEntry)
            .Concat(SplitLinkedDocs(existingRecord.ApDownPaymentInvoiceDocEntry))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var entry in downPaymentKeys)
        {
            if (await TryCancelDownPaymentAsync(entry, operations, preferDocEntry: true) == CancelAttempt.Failed)
                allCancelledInSap = false;
        }

        foreach (var docNum in SplitLinkedDocs(existingRecord.ApDownPaymentInvoiceEntryNumber))
        {
            if (!await TryCancelSapDocumentAsync(docNum, operations))
                allCancelledInSap = false;
        }

        if (!allCancelledInSap)
        {
            operations.Add((false, "SAP cancellation failed. Database record was not updated."));
            return (false, operations);
        }

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                existingRecord.GrossAmount = 0;
                existingRecord.GstAmount = 0;
                existingRecord.Tds = 0;
                existingRecord.Status = StageWisePaymentStatus.Cancelled;
                existingRecord.LastModifiedOn = DateTime.UtcNow;
                context.StageWisePayments.Update(existingRecord);
                if (syncBatchStatus)
                    await SyncBatchStatusForPaymentAsync(existingRecord.Id, StageWisePaymentBatchStatus.Cancelled);
            });
        }
        catch (Exception ex)
        {
            operations.Add((false, $"SAP cancel succeeded but failed to update database: {ex.Message}"));
            return (false, operations);
        }

        operations.Add((true, "Payment amounts cleared and record marked as cancelled."));
        return (true, operations);
    }

    async Task<bool> TryCancelSapDocumentAsync(
        string docEntry,
        List<(bool Success, string Message)> operations)
    {
        var vendor = await TryCancelVendorPaymentAsync(docEntry, operations);
        if (vendor == CancelAttempt.Succeeded)
            return true;
        if (vendor == CancelAttempt.Failed)
            return false;

        var down = await TryCancelDownPaymentAsync(docEntry, operations, preferDocEntry: false);
        return down != CancelAttempt.Failed;
    }

    async Task<CancelAttempt> TryCancelVendorPaymentAsync(
        string key,
        List<(bool Success, string Message)> operations)
    {
        SapVendorPaymentsResponse? vendorByEntry = null;
        try
        {
            vendorByEntry = await sapVendorPaymentService.GetVendorPayment(key);
        }
        catch
        {
            vendorByEntry = null;
        }

        if (vendorByEntry is not null
            && string.IsNullOrEmpty(vendorByEntry.Error?.Message?.Value)
            && vendorByEntry.DocEntry is not null)
        {
            var response = await sapVendorPaymentService.CancelVendorPayment(vendorByEntry.DocEntry.ToString() ?? "");
            var error = response?.Error?.Message?.Value;
            if (!string.IsNullOrEmpty(error) && !IsAlreadyCancelledMessage(error))
            {
                operations.Add((false, $"Failed to cancel vendor payment {key}. SAP Error: {error}"));
                return CancelAttempt.Failed;
            }

            operations.Add((true, $"Vendor payment {key} cancelled in SAP."));
            return CancelAttempt.Succeeded;
        }

        var vendorPayment = await sapVendorPaymentService.GetVendorPaymentByDocEntry(key);
        if (vendorPayment is null
            || !string.IsNullOrEmpty(vendorPayment.Error?.Message?.Value)
            || vendorPayment.Value is not { Count: > 0 })
            return CancelAttempt.NotFound;

        var byNumResponse = await sapVendorPaymentService.CancelVendorPayment(
            vendorPayment.Value.FirstOrDefault()?.DocEntry.ToString() ?? "");
        var byNumError = byNumResponse?.Error?.Message?.Value;
        if (!string.IsNullOrEmpty(byNumError) && !IsAlreadyCancelledMessage(byNumError))
        {
            operations.Add((false, $"Failed to cancel vendor payment {key}. SAP Error: {byNumError}"));
            return CancelAttempt.Failed;
        }

        operations.Add((true, $"Vendor payment {key} cancelled in SAP."));
        return CancelAttempt.Succeeded;
    }

    async Task<CancelAttempt> TryCancelDownPaymentAsync(
        string key,
        List<(bool Success, string Message)> operations,
        bool preferDocEntry)
    {
        string? docEntryToCancel = preferDocEntry ? key : null;
        if (string.IsNullOrWhiteSpace(docEntryToCancel))
        {
            var downPayment = await sapPurchaseDownPaymentService.GetPurchaseDownPaymentByDocNum(key);
            if (downPayment is null
                || !string.IsNullOrEmpty(downPayment.Error?.Message?.Value)
                || downPayment.Value is not { Count: > 0 })
                return CancelAttempt.NotFound;

            docEntryToCancel = downPayment.Value.FirstOrDefault()?.DocEntry.ToString();
        }

        var downPaymentResponse = await sapPurchaseDownPaymentService.CancelDownPayment(docEntryToCancel ?? "");
        var cancelError = downPaymentResponse?.Error?.Message?.Value;
        if (!string.IsNullOrEmpty(cancelError) && !IsAlreadyCancelledMessage(cancelError))
        {
            if (preferDocEntry)
            {
                var byNum = await sapPurchaseDownPaymentService.GetPurchaseDownPaymentByDocNum(key);
                if (byNum?.Value is { Count: > 0 })
                    return await TryCancelDownPaymentAsync(key, operations, preferDocEntry: false);
            }

            operations.Add((false,
                $"Failed to cancel down payment {key}. SAP Error: {cancelError}"));
            return CancelAttempt.Failed;
        }

        operations.Add((true, $"Down payment {key} cancelled in SAP."));
        return CancelAttempt.Succeeded;
    }

    private static List<string> SplitLinkedDocs(string? value) =>
        value?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];

    private static bool IsAlreadyCancelledMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;
        var text = message.ToLowerInvariant();
        return text.Contains("already cancel", StringComparison.Ordinal)
            || text.Contains("already been cancel", StringComparison.Ordinal)
            || text.Contains("is cancelled", StringComparison.Ordinal)
            || text.Contains("is canceled", StringComparison.Ordinal)
            || text.Contains("document cancelled", StringComparison.Ordinal)
            || text.Contains("document canceled", StringComparison.Ordinal);
    }

    private enum CancelAttempt { NotFound, Succeeded, Failed }

    private sealed record PostedDownPayment(int DocEntry, double Amount, bool IsGst);
}