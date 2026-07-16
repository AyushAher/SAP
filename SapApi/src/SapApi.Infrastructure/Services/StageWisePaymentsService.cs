using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services;

public class StageWisePaymentService(
    SapPurchaseDownPaymentService sapPurchaseDownPaymentService,
    SapVendorPaymentService sapVendorPaymentService,
    AppDbContext context,
    IUnitOfWork unitOfWork,
    ICurrentCompanyDbAccessor companyDbAccessor)
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

        var apEntries = new List<int>();
        var approvalRequestIds = new List<int>();

        var entity1 = entity;
        entity1.CompanyDb = CompanyDb;

        SapBaseResponse? sapResponse = null;
        double tdsAmount = 0;
        var hadTdsDeducted = false;
        var tds = existingRecords.FirstOrDefault(x => !string.IsNullOrEmpty(x.ApInvoiceDocEntry) && x.ApInvoiceDocEntry == entity1.ApInvoiceDocEntry)?.Tds;
        hadTdsDeducted = tds != null && tds != 0;

        if (downPaymentAmount > remainingBasicTotal &&
                    (selectedPaymentTermsUdf.Gst == null || selectedPaymentTermsUdf.Gst == 0))
        {
            return (false, "Down payment amount cannot exceed remaining basic amount when GST is 0", null);
        }
        else if (purchaseOrder.DocumentStatus == "bost_Close" || selectedPaymentTermsUdf.Type is "Invoice" or "Retention")
        {
            var (gross, gst) = StageWisePaymentCalculations.SplitAmountForPaymentTerm(
                purchaseOrder, selectedPaymentTermsUdf, downPaymentAmount, totalBasic, existingRecords);
            entity1.GrossAmount = gross;
            entity1.GstAmount = gst;
            (sapResponse, tdsAmount) = await AddToSap(purchaseOrder, selectedPaymentTermsUdf, false, downPaymentAmount, wtCode, desc, entity1.Bank, entity1.ApInvoiceDocEntry, hadTdsDeducted);
            if (sapResponse is not null && sapResponse.PendingApproval)
            {
                entity1.ApprovalRequestId = sapResponse.PendingApprovalRequestId?.ToString();
                entity1.Tds = tdsAmount;
            }
            else if (sapResponse?.Error?.Message?.Value is not null)
            {
                return (false, $"SAP Error: {sapResponse.Error.Message.Value}", null);
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
            var gstPortion = 0.0;
            if (downPaymentAmount > remainingBasicTotal &&
                selectedPaymentTermsUdf.Gst != null &&
                selectedPaymentTermsUdf.Gst != 0)
            {
                gstPortion = Math.Round(downPaymentAmount - remainingBasicTotal, 2);
                entity1.GrossAmount = remainingBasicTotal;
                entity1.GstAmount = gstPortion;
            }
            else
            {
                entity1.GrossAmount = downPaymentAmount;
                entity1.GstAmount = 0;
            }

            var (dpOk, dpMessage, dpTds) = await ApplySeparateDownPaymentsAsync(
                entity1,
                purchaseOrder,
                desc,
                wtCode,
                entity1.GrossAmount ?? 0,
                entity1.GstAmount ?? 0,
                hadTdsDeducted);
            if (!dpOk)
                return (false, dpMessage, null);
            tdsAmount = dpTds;
            entity1.Tds = tdsAmount;
        }
        else if (selectedPaymentTermsUdf.Gst != null && selectedPaymentTermsUdf.Gst != 0)
        {
            if (remainingGstTotal < downPaymentAmount)
                return (false, "GST cannot exceed remaining GST amount", null);
            entity1.GstAmount = downPaymentAmount;
            entity1.GrossAmount = 0;

            var (dpOk, dpMessage, dpTds) = await ApplySeparateDownPaymentsAsync(
                entity1,
                purchaseOrder,
                desc,
                wtCode,
                grossAmount: 0,
                gstAmount: downPaymentAmount,
                hadTdsDeducted);
            if (!dpOk)
                return (false, dpMessage, null);
            tdsAmount = dpTds;
            entity1.Tds = tdsAmount;
        }


        if (string.IsNullOrEmpty(entity1.ApDownPaymentInvoiceEntryNumber)
             && string.IsNullOrEmpty(entity1.ApprovalRequestId))
        {
            return (false, "No records saved in SAP!", null);
        }

        if (!string.IsNullOrEmpty(entity1.ApprovalRequestId))
            entity1.Status = StageWisePaymentStatus.PendingApproval;
        else entity1.Status = StageWisePaymentStatus.Added;

        entity1.StageDesc = desc;
        entity1.WtCode = wtCode;
        entity1.CreatedOn = DateTime.UtcNow;
        entity1.PaymentTermsType = selectedPaymentTermsUdf.Id;
        entity1.LastModifiedOn = DateTime.UtcNow;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await context.StageWisePayments.AddAsync(entity1, ct);
            });
        }
        catch (Exception ex)
        {
            return (false, $"SAP payment succeeded but failed to save locally: {ex.Message}", null);
        }

        if (purchaseOrder.DocumentStatus != "bost_Close"
            && selectedPaymentTermsUdf.Type is not "Invoice" or "Retention"
            && HasCompleteDownPaymentDocs(entity1, entity1.GrossAmount ?? 0, entity1.GstAmount ?? 0))
        {
            var paymentInvoices = BuildDownPaymentInvoices(
                entity1.DownPaymentDocEntry,
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
        const bool hadTdsDeducted = false;

        var entity = new StageWisePayment
        {
            CompanyDb = CompanyDb,
            DocNumber = purchaseOrder.DocNum,
            Bank = bank,
            WtCode = wtCode,
            GrossAmount = totalGross,
            GstAmount = totalGst,
            StageDesc = batchDesc,
            Stage = StageWisePaymentStages.AfterReceiptOfMaterial,
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = DateTime.UtcNow,
        };

        // Separate SAP AP Down Payments for Basic and GST; one Outgoing Payment covers both.
        var (dpOk, dpMessage, tdsAmount) = await ApplySeparateDownPaymentsAsync(
            entity,
            purchaseOrder,
            batchDesc,
            wtCode,
            totalGross,
            totalGst,
            hadTdsDeducted);
        if (!dpOk)
            return (false, dpMessage, null);

        entity.Tds = tdsAmount;
        if (!string.IsNullOrEmpty(entity.ApprovalRequestId))
            entity.Status = StageWisePaymentStatus.PendingApproval;
        else if (!string.IsNullOrWhiteSpace(entity.ApDownPaymentInvoiceEntryNumber))
            entity.Status = StageWisePaymentStatus.Added;
        else
            return (false, "No records saved in SAP!", null);

        if (purchaseOrder.DocumentStatus != "bost_Close"
            && HasCompleteDownPaymentDocs(entity, totalGross, totalGst))
        {
            var paymentInvoices = BuildDownPaymentInvoices(
                entity.DownPaymentDocEntry,
                totalGross,
                totalGst,
                entity.Tds ?? 0);
            var netOutgoing = Math.Round(paymentInvoices.Sum(x => x.SumApplied), 2);
            var (outgoingResponse, _) = await AddOutgoingPayment(
                purchaseOrder,
                bank,
                netOutgoing,
                paymentInvoices,
                userRemark);

            if (outgoingResponse?.PendingApproval == true)
            {
                entity.ApprovalRequestId = AppendApprovalRequestId(
                    entity.ApprovalRequestId,
                    outgoingResponse.PendingApprovalRequestId?.ToString());
                entity.Status = StageWisePaymentStatus.PendingApproval;
            }
            else if (outgoingResponse?.Error?.Message?.Value is not null)
            {
                return (false, $"SAP Error: {outgoingResponse.Error.Message.Value}", null);
            }
            else if (outgoingResponse?.BaseDocNum is not null)
            {
                ApplyOutgoingPaymentResult(entity, outgoingResponse);
            }
        }

        if (!persist)
            return (true, "Payment prepared successfully", entity);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await context.StageWisePayments.AddAsync(entity, ct);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            return (false, $"SAP payment succeeded but failed to save locally: {ex.Message}", null);
        }

        return (true, "Payment created successfully", entity);
    }

    private async Task<(bool Ok, string Message, double TdsAmount)> ApplySeparateDownPaymentsAsync(
        StageWisePayment entity,
        SapPurchaseOrdersResponse purchaseOrder,
        string? desc,
        string? wtCode,
        double grossAmount,
        double gstAmount,
        bool hadTdsDeducted)
    {
        var docNums = new List<string>();
        var docEntries = new List<string>();
        var approvalIds = new List<string>();
        double tdsAmount = 0;

        if (grossAmount > 0)
        {
            var (sapResponse, basicTds) = await AddDownPayment(
                purchaseOrder, isGst: false, grossAmount, wtCode, $"{desc} (Basic)", hadTdsDeducted);

            if (sapResponse?.PendingApproval == true)
            {
                if (sapResponse.PendingApprovalRequestId.HasValue)
                    approvalIds.Add(sapResponse.PendingApprovalRequestId.Value.ToString());
                tdsAmount += basicTds;
            }
            else if (sapResponse?.Error?.Message?.Value is not null)
            {
                return (false, $"SAP Error: {sapResponse.Error.Message.Value}", 0);
            }
            else if (sapResponse?.BaseDocEntry.HasValue == true)
            {
                if (sapResponse.BaseDocNum.HasValue)
                    docNums.Add(sapResponse.BaseDocNum.Value.ToString());
                docEntries.Add(sapResponse.BaseDocEntry.Value.ToString());
                tdsAmount += basicTds;
            }
            else
            {
                return (false, "No Basic down payment was created in SAP.", 0);
            }
        }

        if (gstAmount > 0)
        {
            var (sapResponse, _) = await AddDownPayment(
                purchaseOrder, isGst: true, gstAmount, wtCode, $"{desc} (GST)", hadTdsDeducted);

            if (sapResponse?.PendingApproval == true)
            {
                if (sapResponse.PendingApprovalRequestId.HasValue)
                    approvalIds.Add(sapResponse.PendingApprovalRequestId.Value.ToString());
            }
            else if (sapResponse?.Error?.Message?.Value is not null)
            {
                return (false, $"SAP Error: {sapResponse.Error.Message.Value}", 0);
            }
            else if (sapResponse?.BaseDocEntry.HasValue == true)
            {
                if (sapResponse.BaseDocNum.HasValue)
                    docNums.Add(sapResponse.BaseDocNum.Value.ToString());
                docEntries.Add(sapResponse.BaseDocEntry.Value.ToString());
            }
            else
            {
                return (false, "No GST down payment was created in SAP.", 0);
            }
        }

        if (docNums.Count > 0)
            entity.ApDownPaymentInvoiceEntryNumber = string.Join(',', docNums);
        if (docEntries.Count > 0)
        {
            entity.DownPaymentDocEntry = string.Join(',', docEntries);
            entity.ApDownPaymentInvoiceDocEntry = entity.DownPaymentDocEntry;
        }
        if (approvalIds.Count > 0)
            entity.ApprovalRequestId = string.Join(',', approvalIds);

        return (true, string.Empty, Math.Round(tdsAmount, 2));
    }

    private static bool HasCompleteDownPaymentDocs(StageWisePayment entity, double grossAmount, double gstAmount)
    {
        var expected = (grossAmount > 0 ? 1 : 0) + (gstAmount > 0 ? 1 : 0);
        if (expected == 0)
            return false;

        var count = entity.DownPaymentDocEntry?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length ?? 0;
        return count == expected;
    }

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

    private async Task<(SapBaseResponse? response, double tdsAmount)> AddToSap(
        SapPurchaseOrdersResponse purchaseOrder,
        PaymentTermsUdf paymentTerms,
        bool isGst,
        double amount,
        string? wtCode,
        string? desc, string? bank, string? apInvoiceDoc, bool hadTdsDeducted)
    {
        if (purchaseOrder.DocumentStatus == "bost_Close" || paymentTerms?.Type is "Invoice" or "Retention")
            return await AddOutgoingPayment(purchaseOrder, bank, amount, apInvoiceDoc, hadTdsDeducted);
        return await AddDownPayment(purchaseOrder, isGst, amount, wtCode, desc, hadTdsDeducted);
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
        string? userRemark = null)
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

        var sapResponse = await sapVendorPaymentService.CreateVendorPayments(new SapVendorPaymentRequests
        {
            CardCode = purchaseOrder?.CardCode ?? "",
            TransferAccount = bank ?? "_SYS00000000980",
            TransferDate = DateTime.UtcNow,
            TransferSum = transferSum.ToString("F2"),
            ProjectCode = purchaseOrder?.Project,
            PoNumber = purchaseOrder?.DocNum?.ToString() ?? "",
            Remarks = Constants.PaymentRemarks.Build(
                userRemark, purchaseOrder?.BPLId, purchaseOrder?.DocNum?.ToString()),
            PaymentInvoices = paymentInvoices.ToList(),
            BPLId = purchaseOrder?.BPLId ?? 1,
        }, supportingData: purchaseOrder?.DocEntry.ToString());

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
        string? desc, bool hadTdsDeducted)
    {
        var documentLines = purchaseOrder.DocumentLines ?? [];

        foreach (var line in documentLines)
        {
            line.WTLiable = isGst ? Constants.SapBoolean.SapFalse : Constants.SapBoolean.SapTrue;
            line.TaxLiable = Constants.SapBoolean.SapFalse;
            line.BaseEntry = purchaseOrder.DocEntry;
            line.BaseType = 22;
            line.BaseLine = line.LineNum;
        }

        var req = new SapPurchaseDownPaymentRequest
        {
            DocumentLines = documentLines,
            CardCode = purchaseOrder?.CardCode,
            DownPayment = amount,
            DocType = purchaseOrder?.DocType,
            DocTotal = amount,
            BPLId = purchaseOrder?.BPLId ?? 1,
            Comments = $"{desc} against PO {purchaseOrder?.DocNum}",
        };

        if (!isGst)
        {
            req.WithholdingTaxDataCollection =
            [
                new SapWithholdingTaxDataCollectionResponse
                {
        //            TaxableAmount = amount,
                    WtCode = wtCode
                }
            ];
        }

        var sapResponse = await sapPurchaseDownPaymentService.SaveDownPayment(req, supportingData: purchaseOrder?.DocEntry.ToString());
        double tdsAmount = 0;
        if (sapResponse is not null)
        {
            sapResponse.BaseDocEntry = sapResponse.DocEntry;
            sapResponse.BaseDocNum = sapResponse.DocNum;
            tdsAmount = hadTdsDeducted ? 0 : sapResponse.WTAmount ?? 0;
        }
        return (sapResponse, tdsAmount);
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
            context.StageWisePayments.Update(record);
            await SyncBatchStatusForPaymentAsync(record.Id, StageWisePaymentBatchStatus.Rejected);
        }

        await unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask);
    }

    public async Task MarkApprovedWhenAllRequestsCompleteAsync(int approvalRequestId)
    {
        var approvalRequestIdStr = approvalRequestId.ToString();
        var records = await context.StageWisePayments
            .Where(x => x.CompanyDb == CompanyDb && x.ApprovalRequestId != null && x.Status == StageWisePaymentStatus.PendingApproval)
            .ToListAsync();

        foreach (var record in records)
        {
            if (!IsLinkedToApprovalRequest(record, approvalRequestIdStr))
                continue;

            var requestIds = ParseApprovalRequestIds(record.ApprovalRequestId);
            if (requestIds.Count == 0)
                continue;

            var statuses = await context.ApprovalRequests
                .Where(r => r.CompanyDb == CompanyDb && requestIds.Contains(r.Id))
                .Select(r => r.OverallStatus)
                .ToListAsync();

            if (statuses.Count != requestIds.Count)
                continue;

            if (statuses.All(s => s == ApprovalStatus.Approved))
            {
                record.Status = StageWisePaymentStatus.Approved;
                record.LastModifiedOn = DateTime.UtcNow;
                context.StageWisePayments.Update(record);
                await SyncBatchStatusForPaymentAsync(record.Id, StageWisePaymentBatchStatus.Approved);
            }
        }

        await unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask);
    }

    private async Task SyncBatchStatusForPaymentAsync(int stageWisePaymentId, StageWisePaymentBatchStatus status)
    {
        var batch = await context.StageWisePaymentBatches
            .FirstOrDefaultAsync(b => b.CompanyDb == CompanyDb
                && (b.StageWisePaymentId == stageWisePaymentId
                    || b.DownPaymentStageWisePaymentId == stageWisePaymentId));
        if (batch is null)
            return;

        batch.Status = status;
        batch.LastModifiedOn = DateTime.UtcNow;
        context.StageWisePaymentBatches.Update(batch);
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

        var docEntries = (existingRecord.ApDownPaymentInvoiceEntryNumber?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList() ?? []);

        docEntries.Reverse();

        if (docEntries.Count == 0)
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
        // Linked SAP docs are stored as: [Basic DP Num], [GST DP Num], [Outgoing Payment Num]
        // Cancel outgoing payment first, then down payments.
        if (docEntries.Count > 3)
        {
            operations.Add((false, "Invalid number of SAP documents linked to this record."));
            return (false, operations);
        }

        if (docEntries.Count >= 2)
        {
            if (!await TryCancelSapDocumentAsync(docEntries[0], operations, "vp"))
                allCancelledInSap = false;

            for (var i = 1; i < docEntries.Count; i++)
            {
                if (!await TryCancelSapDocumentAsync(docEntries[i], operations, "dp"))
                    allCancelledInSap = false;
            }
        }
        else
        {
            foreach (var docEntry in docEntries)
            {
                if (!await TryCancelSapDocumentAsync(docEntry, operations))
                    allCancelledInSap = false;
            }
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
        List<(bool Success, string Message)> operations,
        string? documentType = null)
    {
        if (documentType is null or "vp")
        {
            var vendorPayment = await sapVendorPaymentService.GetVendorPaymentByDocEntry(docEntry);
            if (vendorPayment is not null && string.IsNullOrEmpty(vendorPayment.Error?.Message?.Value) && vendorPayment.Value != null && vendorPayment.Value.Count != 0)
            {
                var response = await sapVendorPaymentService.CancelVendorPayment(vendorPayment.Value?.FirstOrDefault()?.DocEntry.ToString() ?? "");
                if (!string.IsNullOrEmpty(response?.Error?.Message?.Value))
                {
                    operations.Add((false,
                        $"Failed to cancel vendor payment {docEntry}. SAP Error: {response?.Error?.Message?.Value ?? "Unknown error"}"));
                    return false;
                }

                operations.Add((true, $"Vendor payment {docEntry} cancelled in SAP."));
                return true;
            }

            if (documentType == "vp")
            {
                operations.Add((false, $"No vendor payment found for document {docEntry}."));
                return false;
            }
        }

        if (documentType is null or "dp")
        {
            var downPayment = await sapPurchaseDownPaymentService.GetPurchaseDownPaymentByDocNum(docEntry);
            if (downPayment is null || !string.IsNullOrEmpty(downPayment.Error?.Message?.Value) || downPayment.Value == null || downPayment.Value.Count == 0)
            {
                operations.Add((false,
                    $"No vendor payment or down payment found for document entry {docEntry}. SAP Error: {downPayment?.Error?.Message?.Value ?? "Unknown error"}"));
                return false;
            }

            var downPaymentResponse = await sapPurchaseDownPaymentService.CancelDownPayment(downPayment.Value.FirstOrDefault()?.DocEntry.ToString() ?? "");
            if (!string.IsNullOrEmpty(downPaymentResponse?.Error?.Message?.Value))
            {
                operations.Add((false,
                    $"Failed to cancel down payment {docEntry}. SAP Error: {downPaymentResponse?.Error?.Message?.Value ?? "Unknown error"}"));
                return false;
            }

            operations.Add((true, $"Down payment {docEntry} cancelled in SAP."));
            return true;
        }

        return false;
    }
}