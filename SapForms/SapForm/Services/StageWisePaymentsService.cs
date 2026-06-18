using SapForm.Services;
using Shared;
using Shared.Entities;
using Shared.Requests;
using Shared.Responses.Sap;

public class StageWisePaymentService(
    SapPurchaseDownPaymentService sapPurchaseDownPaymentService,
    SapVendorPaymentService sapVendorPaymentService,
    Context context)
{
    public async Task<(bool IsSuccess, string Message)> CreateStageWisePayment(
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
            return (false, "Purchase order not found!");

        if (selectedPaymentTermsUdf is null)
            return (false, "Payment term not selected!");

        if (downPaymentAmount <= 0)
            return (false, "Down payment amount cannot be less than or equal to 0!");

        var paidBasicTotal = existingRecords
            .Where(x => x.PaymentTermsType == selectedPaymentTermsUdf.Id)
            .Sum(x => x.GrossAmount);

        var paidGstTotal = existingRecords
            .Where(x => x.PaymentTermsType == selectedPaymentTermsUdf.Id)
            .Sum(x => x.GstAmount);

        if (downPaymentAmount > (purchaseOrder.DocTotal ?? 0))
            return (false, "Down payment amount cannot be more than total PO value");

        var remainingGstTotal =
            (((purchaseOrder.VatSum ?? 0) * (selectedPaymentTermsUdf.Gst ?? 0)) / 100)
            - (paidGstTotal ?? 0);

        var remainingBasicTotal =
            ((totalBasic * (selectedPaymentTermsUdf.Basic ?? 0)) / 100)
            - (paidBasicTotal ?? 0);

        if (downPaymentAmount > payableAmount)
            return (false, "Down payment amount cannot exceed the payable amount for the stage.");

        var apEntries = new List<int>();
        var approvalRequestIds = new List<int>();

        var entity1 = entity;

        StageWisePayment? entity2 = null;
        SapBaseResponse? sapResponse = null;
        double tdsAmount = 0;
        var hadTdsDeducted = false;
        var tds = existingRecords.FirstOrDefault(x => !string.IsNullOrEmpty(x.ApInvoiceDocEntry) && x.ApInvoiceDocEntry == entity1.ApInvoiceDocEntry)?.Tds;
        hadTdsDeducted = tds != null && tds != 0;

        if (downPaymentAmount > remainingBasicTotal &&
                    (selectedPaymentTermsUdf.Gst == null || selectedPaymentTermsUdf.Gst == 0))
        {
            return (false, "Down payment amount cannot exceed remaining basic amount when GST is 0");
        }
        else if (purchaseOrder.DocumentStatus == "bost_Close" || selectedPaymentTermsUdf.Type is "Invoice" or "Retention")
        {
            entity1.GrossAmount = downPaymentAmount;
            (sapResponse, tdsAmount) = await AddToSap(purchaseOrder, selectedPaymentTermsUdf, false, entity1.GrossAmount ?? 0, wtCode, desc, entity1.Bank, entity1.ApInvoiceDocEntry, hadTdsDeducted);
            if (sapResponse is not null && sapResponse.PendingApproval)
            {
                entity1.ApprovalRequestId = sapResponse.PendingApprovalRequestId?.ToString();
                entity1.Tds = tdsAmount;
            }
            else if (sapResponse?.Error?.Message?.Value is not null)
            {
                return (false, $"SAP Error: {sapResponse.Error.Message.Value}");
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
                entity2 = new StageWisePayment
                {
                    Bank = entity.Bank,
                    GrossAmount = 0,
                    DocNumber = entity.DocNumber,
                    ApInvoiceDocEntry = entity.ApInvoiceDocEntry,
                    PaymentTermsType = selectedPaymentTermsUdf.Id,
                    GstAmount = downPaymentAmount - remainingBasicTotal,
                };

                entity1.GrossAmount = remainingBasicTotal;
                (sapResponse, tdsAmount) = await AddToSap(purchaseOrder, selectedPaymentTermsUdf, true, entity2.GstAmount ?? 0, wtCode, desc, entity2.Bank, entity2.ApInvoiceDocEntry, hadTdsDeducted);

                if (sapResponse?.PendingApproval == true)
                    entity2.ApprovalRequestId = sapResponse.PendingApprovalRequestId?.ToString();
                else if (sapResponse?.Error?.Message?.Value is not null)
                    return (false, $"SAP Error: {sapResponse.Error.Message.Value}");
                else if (sapResponse?.BaseDocEntry.HasValue == true)
                {
                    entity2.ApDownPaymentInvoiceEntryNumber = sapResponse.BaseDocNum?.ToString();
                    entity2.Tds = tdsAmount;
                    entity2.ApDownPaymentInvoiceDocEntry = sapResponse.BaseDocEntry?.ToString();
                }
            }
            else
            {
                entity1.GrossAmount = downPaymentAmount;
            }

            (sapResponse, tdsAmount) = await AddToSap(purchaseOrder, selectedPaymentTermsUdf, false, entity1.GrossAmount ?? 0, wtCode, desc, entity1.Bank, entity1.ApInvoiceDocEntry, hadTdsDeducted);

            if (sapResponse?.PendingApproval == true)
                entity1.ApprovalRequestId = sapResponse.PendingApprovalRequestId?.ToString();
            else if (sapResponse?.Error?.Message?.Value is not null)
            {
                return (false, $"SAP Error: {sapResponse.Error.Message.Value}");
            }
            else if (sapResponse?.BaseDocEntry.HasValue == true)
            {
                entity1.ApDownPaymentInvoiceEntryNumber = sapResponse.BaseDocNum?.ToString();
                entity1.Tds = tdsAmount;
                entity1.ApDownPaymentInvoiceDocEntry = sapResponse.BaseDocEntry?.ToString();
            }

        }
        else if (selectedPaymentTermsUdf.Gst != null && selectedPaymentTermsUdf.Gst != 0)
        {
            if (remainingGstTotal < downPaymentAmount)
                return (false, "GST cannot exceed remaining GST amount");
            entity1.GstAmount = downPaymentAmount;
            (sapResponse, tdsAmount) = await AddToSap(purchaseOrder, selectedPaymentTermsUdf, true, downPaymentAmount, wtCode, desc, entity1.Bank, entity1.ApInvoiceDocEntry, hadTdsDeducted);
            SapPurchaseDownPaymentResponse? sapPurchaseDownPaymentResponse = sapResponse as SapPurchaseDownPaymentResponse;
            if (sapResponse?.PendingApproval == true)
                entity1.ApprovalRequestId = sapResponse.PendingApprovalRequestId?.ToString();
            else if (sapResponse?.Error?.Message?.Value is not null)
            {
                return (false, $"SAP Error: {sapResponse.Error.Message.Value}");
            }
            else if (sapResponse?.BaseDocEntry.HasValue == true)
            {
                entity1.ApDownPaymentInvoiceEntryNumber = sapResponse.BaseDocNum?.ToString();
                entity1.ApDownPaymentInvoiceDocEntry = sapResponse.BaseDocEntry?.ToString();
                entity1.Tds = tdsAmount;
            }

        }


        if (string.IsNullOrEmpty(entity1.ApDownPaymentInvoiceEntryNumber)
             && string.IsNullOrEmpty(entity2?.ApDownPaymentInvoiceEntryNumber)
             && string.IsNullOrEmpty(entity1.ApprovalRequestId)
              && string.IsNullOrEmpty(entity2?.ApprovalRequestId))
        {
            return (false, "No records saved in SAP!");
        }

        if (!string.IsNullOrEmpty(entity1.ApprovalRequestId))
            entity1.Status = StageWisePaymentStatus.PendingApproval;
        else entity1.Status = StageWisePaymentStatus.Added;

        if (entity2 is not null)
        {
            if (!string.IsNullOrEmpty(entity2.ApprovalRequestId))
                entity2.Status = StageWisePaymentStatus.PendingApproval;
            else entity2.Status = StageWisePaymentStatus.Added;
            entity2.StageDesc = desc;
            entity2.WtCode = wtCode;
            entity2.CreatedOn = DateTime.Now;
            entity2.PaymentTermsType = selectedPaymentTermsUdf.Id;
            entity2.LastModifiedOn = DateTime.Now;
            await context.StageWisePayments.AddAsync(entity2);
        }

        entity1.StageDesc = desc;
        entity1.WtCode = wtCode;
        entity1.CreatedOn = DateTime.Now;
        entity1.PaymentTermsType = selectedPaymentTermsUdf.Id;
        entity1.LastModifiedOn = DateTime.Now;

        await context.StageWisePayments.AddAsync(entity1);
        await context.SaveChangesAsync();

        var recordsArray = new[] {
                entity1,
                entity2
            }.Where(x => x is not null).Select(x => x!).ToList();

        if (purchaseOrder.DocumentStatus != "bost_Close" && selectedPaymentTermsUdf.Type is not "Invoice" or "Retention")
        {
            foreach (var record in recordsArray)
            {
                var (createOutgoingPaymentForDownPayment, _) = await AddOutgoingPayment(purchaseOrder, entity.Bank, ((record.GrossAmount ?? record.GstAmount ?? 0) - (record.Tds ?? 0)),
                                         record.ApDownPaymentInvoiceDocEntry, hadTdsDeducted, Constants.SapVendorPaymentInvoiceType.DownPayment);

                if (createOutgoingPaymentForDownPayment?.PendingApproval == true)
                {
                    if (record.ApprovalRequestId is null)
                        record.ApprovalRequestId = createOutgoingPaymentForDownPayment.PendingApprovalRequestId?.ToString();
                    else record.ApprovalRequestId = record.ApprovalRequestId + "," + createOutgoingPaymentForDownPayment.PendingApprovalRequestId?.ToString();

                    record.Status = StageWisePaymentStatus.PendingApproval;
                    context.StageWisePayments.Update(record);
                    await context.SaveChangesAsync();
                }
            }
        }

        return (true, "Payment created successfully");
        //var hasTds = double.TryParse(sapResponse?.SupportingData ?? "0", out var tdsAmount) && tdsAmount > 0;
        //entity.Tds = hasTds ? tdsAmount : 0;
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
        double amount, string? apInvoiceDoc, bool hadTdsDeducted, string? invoiceType = Constants.SapVendorPaymentInvoiceType.Invoice)
    {

        var apInvoices = await sapVendorPaymentService.GetApInvoices(purchaseOrder?.CardCode ?? "");
        var apInvoice = apInvoices?.Value?.Where(x => x.DocEntry.ToString() == apInvoiceDoc).FirstOrDefault();

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

        var sapResponse = await sapVendorPaymentService.CreateVendorPayments(new SapVendorPaymentRequests
        {
            CardCode = purchaseOrder?.CardCode ?? "",
            TransferAccount = bank ?? "_SYS00000000980",
            TransferDate = DateTime.Now,
            TransferSum = net.ToString(),
            ProjectCode = purchaseOrder?.Project,
            PoNumber = purchaseOrder?.DocNum?.ToString() ?? "",
            PaymentInvoices = [
                new PaymentInvoice  {
                    DocEntry = apInvoice?.DocEntry ?? int.Parse(apInvoiceDoc ?? "0"),
                    InvoiceType  = invoiceType,
                    AppliedFC = 0 ,
                    LineNumber = 0,
                    SumApplied  = net
                }
            ],
            BPLId = purchaseOrder?.BPLId ?? 1,
        }, supportingData: purchaseOrder?.DocEntry.ToString());

        if (sapResponse is not null)
        {
            sapResponse.BaseDocEntry = sapResponse.DocEntry;
            sapResponse.BaseDocNum = sapResponse.DocNumber;
            sapResponse.SupportingData = (apInvoice?.WTAmount ?? 0).ToString();
        }
        return (sapResponse, hadTdsDeducted ? 0 : apInvoice?.WTAmount ?? 0);
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
    public async Task MarkApprovedWhenAllRequestsCompleteAsync(int approvalRequestId)
    {
        var approvalRequestIdStr = approvalRequestId.ToString();
        var records = await context.StageWisePayments
            .Where(x => x.ApprovalRequestId != null && x.Status == StageWisePaymentStatus.PendingApproval)
            .ToListAsync();

        foreach (var record in records)
        {
            if (!IsLinkedToApprovalRequest(record, approvalRequestIdStr))
                continue;

            var requestIds = ParseApprovalRequestIds(record.ApprovalRequestId);
            if (requestIds.Count == 0)
                continue;

            var statuses = await context.ApprovalRequests
                .Where(r => requestIds.Contains(r.Id))
                .Select(r => r.OverallStatus)
                .ToListAsync();

            if (statuses.Count != requestIds.Count)
                continue;

            if (statuses.All(s => s == ApprovalStatus.Approved))
            {
                record.Status = StageWisePaymentStatus.Approved;
                record.LastModifiedOn = DateTime.UtcNow;
                context.StageWisePayments.Update(record);
            }
        }

        await context.SaveChangesAsync();
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

        context.StageWisePayments.Remove(record);

        var recordApprovalRequests = record.ApprovalRequestId?.Split(",").ToList() ?? [];
        var approvalRequests = context.ApprovalRequests.Where(x => record.ApprovalRequestId != null
            && recordApprovalRequests.Contains(x.Id.ToString())).ToList();
        context.ApprovalRequests.RemoveRange(approvalRequests);
        await context.SaveChangesAsync();
        return (true, "Stage wise payment record deleted successfully.");
    }

    public async Task<(bool Success, IReadOnlyList<(bool Success, string Message)> Operations)> CancelOutgoingPayment(
        StageWisePayment record)
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

        if (docEntries is null || docEntries.Count == 0)
        {
            operations.Add((false, "No SAP documents linked to this record. Cannot cancel in SAP."));
            return (false, operations);
        }

        var allCancelledInSap = true;

        if (docEntries.Count > 2)
        {
            operations.Add((false, $"Invalid number of entries"));
            return (false, operations);
        }

        if (docEntries.Count == 2)
        {
            if (!await TryCancelSapDocumentAsync(docEntries[0], operations, "vp"))
                allCancelledInSap = false;
            if (!await TryCancelSapDocumentAsync(docEntries[1], operations, "dp"))
                allCancelledInSap = false;
        }

        if (docEntries.Count == 1)
        {
            if (!await TryCancelSapDocumentAsync(docEntries[0], operations, "dp"))
                allCancelledInSap = false;
        }

        if (!allCancelledInSap)
        {
            operations.Add((false, "SAP cancellation failed. Database record was not updated."));
            return (false, operations);
        }

        existingRecord.GrossAmount = 0;
        existingRecord.GstAmount = 0;
        existingRecord.Tds = 0;
        existingRecord.Status = StageWisePaymentStatus.Cancelled;
        existingRecord.LastModifiedOn = DateTime.UtcNow;
        context.StageWisePayments.Update(existingRecord);
        await context.SaveChangesAsync();

        operations.Add((true, "Payment amounts cleared and record marked as cancelled."));
        return (true, operations);
    }

    async Task<bool> TryCancelSapDocumentAsync(string docEntry, List<(bool Success, string Message)> operations, string type = "dp")
    {
        if (type == "vp")
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
        }

        if (type == "dp")
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