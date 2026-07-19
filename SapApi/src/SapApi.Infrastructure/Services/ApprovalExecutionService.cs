using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services;

public class ApprovalExecutionService(
    AppDbContext context,
    IUnitOfWork unitOfWork,
    InventoryItemsTransferService inventoryItemsTransferService,
    SapInventoryGenExitsService sapInventoryGenExitsService,
    SapProductionOrdersService sapProductionOrdersService,
    SapPurchaseOrderService sapPurchaseOrderService,
    SapPurchaseDownPaymentService sapPurchaseDownPaymentService,
    SapVendorPaymentService sapVendorPaymentService,
    StageWisePaymentService stageWisePaymentService,
    ApprovalService approvalService)
{
    public async Task<SapBaseResponse?> ExecuteAsync(ApprovalRequest request, ApprovalActionData? data, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.RequestBody))
            return null;

        SapBaseResponse? sapBaseResponse = null;
        var utrNo = data?.UtrNo;
        var utrDate = data?.UtrDate;
        var comment = data?.Comment;

        switch (request.DocumentType)
        {
            case ApprovalDocumentType.InventoryItemsTransfer:
            {
                var body = JsonSerializer.Deserialize<SapInventoryTransferRequestsRequest>(request.RequestBody);
                if (body == null) return sapBaseResponse;
                sapBaseResponse = request.Action == ApprovalAction.Create
                    ? await inventoryItemsTransferService.CreateRequest(body, request.Id)
                    : await inventoryItemsTransferService.UpdateRequest(body, body.DocEntry?.ToString() ?? "", request.Id);
                break;
            }
            case ApprovalDocumentType.IssueForProduction:
            {
                var body = JsonSerializer.Deserialize<SapInventoryGenExitRequestOrderRequest>(request.RequestBody);
                if (body == null) return sapBaseResponse;
                if (request.Action == ApprovalAction.Create)
                    sapBaseResponse = await sapInventoryGenExitsService.CreateAsync(body, request.Id);
                break;
            }
            case ApprovalDocumentType.ProductionOrder:
            {
                var body = JsonSerializer.Deserialize<SapProductionOrdersResponse>(request.RequestBody);
                if (body == null) return sapBaseResponse;
                sapBaseResponse = request.Action switch
                {
                    ApprovalAction.Update => await sapProductionOrdersService.UpdateProductionOrderAsync(body, request.Id),
                    ApprovalAction.Create => await sapProductionOrdersService.CreateProductionOrderAsync(body, request.Id),
                    _ => sapBaseResponse
                };
                break;
            }
            case ApprovalDocumentType.PurchaseOrder:
            {
                var body = JsonSerializer.Deserialize<SapPurchaseOrdersResponse>(request.RequestBody);
                if (body == null) return sapBaseResponse;
                sapBaseResponse = request.Action switch
                {
                    ApprovalAction.Create => await sapPurchaseOrderService.CreatePurchaseOrder(body, request.Id),
                    ApprovalAction.Update => await sapPurchaseOrderService.UpdatePurchaseOrder(body, request.Id),
                    _ => sapBaseResponse
                };
                break;
            }
            case ApprovalDocumentType.StagewisePayments_DP:
            {
                var body = JsonSerializer.Deserialize<SapPurchaseDownPaymentRequest>(request.RequestBody);
                if (body == null) return sapBaseResponse;
                if (utrDate is not null) body.DocDueDate = utrDate ?? DateTime.Now;

                if (request.Action == ApprovalAction.Create)
                {
                    var dpResponse = await sapPurchaseDownPaymentService.SaveDownPayment(body, request.Id);
                    sapBaseResponse = dpResponse;
                    var docEntry = dpResponse?.DocEntry?.ToString() ?? "";
                    var docNumber = dpResponse?.DocNum?.ToString() ?? "";

                    if (string.IsNullOrEmpty(sapBaseResponse?.Error?.Message?.Value))
                    {
                        var records = await GetStageWisePaymentsLinkedToApprovalAsync(request.Id, request.CompanyDb, cancellationToken);

                        foreach (var item in records)
                        {
                            if (string.IsNullOrEmpty(item.ApDownPaymentInvoiceEntryNumber))
                                item.ApDownPaymentInvoiceEntryNumber = dpResponse?.DocNum?.ToString();
                            else
                                item.ApDownPaymentInvoiceEntryNumber += "," + dpResponse?.DocNum;
                            context.StageWisePayments.Update(item);
                        }
                        await unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask, cancellationToken);
                    }

                    if (sapBaseResponse is not null)
                    {
                        sapBaseResponse.ApprovalDocEntry = docEntry;
                        sapBaseResponse.ApprovalDocNumber = docNumber;
                    }
                }
                break;
            }
            case ApprovalDocumentType.Payments:
            {
                var body = JsonSerializer.Deserialize<SapVendorPaymentRequests>(request.RequestBody);
                var record = await FindStageWisePaymentForApprovalAsync(request);
                if (body is not null && record is not null)
                {
                    var batch = await context.StageWisePaymentBatches
                        .FirstOrDefaultAsync(b =>
                            b.ApprovalRequestId == request.Id.ToString()
                            || b.StageWisePaymentId == record.Id
                            || b.DownPaymentStageWisePaymentId == record.Id,
                            cancellationToken);

                    var paymentDate = batch?.PaymentDate ?? utrDate ?? DateTime.Now;
                    var postingDate = batch?.PostingDate ?? utrDate ?? paymentDate;
                    var reference = !string.IsNullOrWhiteSpace(utrNo)
                        ? utrNo
                        : (batch?.ReferenceNo ?? string.Empty);
                    var userRemark = !string.IsNullOrWhiteSpace(batch?.JournalRemark)
                        ? batch.JournalRemark
                        : comment;

                    body.TransferReference = reference ?? "";
                    body.CounterReference = reference ?? "";
                    body.TransferDate = paymentDate;
                    body.DocDueDate = paymentDate;
                    body.DocDate = paymentDate;
                    body.PostingDate = postingDate;
                    body.Remarks = Constants.PaymentRemarks.Build(
                        userRemark, body.BPLId, body.PoNumber);
                    body.JournalRemarks = null;

                    if (!string.IsNullOrWhiteSpace(batch?.Account))
                    {
                        var mode = batch.ModeOfPayment ?? Constants.SapPaymentMeansType.BankTransfer;
                        switch (mode)
                        {
                            case Constants.SapPaymentMeansType.Cash:
                                body.CashAccount = batch.Account;
                                break;
                            case Constants.SapPaymentMeansType.Check:
                                body.CheckAccount = batch.Account;
                                break;
                            default:
                                body.TransferAccount = batch.Account;
                                break;
                        }

                        if (body.CashFlowAssignments.Count > 0)
                        {
                            body.CashFlowAssignments[0].PaymentMeans = mode;
                            // Never leave CashFlowLineItemID as 0 — SAP rejects it (3741-3).
                            if (body.CashFlowAssignments[0].CashFlowLineItemID == 0)
                                body.CashFlowAssignments.Clear();
                        }
                    }
                }

                if (body == null) return sapBaseResponse;
                if (request.Action == ApprovalAction.Create)
                {
                    var dpResponse = await sapVendorPaymentService.CreateVendorPayments(body, request.Id);
                    sapBaseResponse = dpResponse;
                    var docEntry = dpResponse?.DocEntry?.ToString() ?? "";
                    var docNumber = dpResponse?.DocNumber?.ToString() ?? "";

                    if (string.IsNullOrEmpty(sapBaseResponse?.Error?.Message?.Value))
                    {
                        var records = await GetStageWisePaymentsLinkedToApprovalAsync(request.Id, request.CompanyDb, cancellationToken);

                        foreach (var item in records)
                        {
                            if (string.IsNullOrEmpty(item.ApDownPaymentInvoiceEntryNumber))
                                item.ApDownPaymentInvoiceEntryNumber = dpResponse?.DocNumber?.ToString();
                            else
                                item.ApDownPaymentInvoiceEntryNumber += "," + dpResponse?.DocNumber;
                            item.PaymentDocEntry = dpResponse?.DocEntry?.ToString();
                            context.StageWisePayments.Update(item);
                        }
                        await unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask, cancellationToken);
                    }

                    if (sapBaseResponse is not null)
                    {
                        sapBaseResponse.ApprovalDocEntry = docEntry;
                        sapBaseResponse.ApprovalDocNumber = docNumber;
                    }
                }
                break;
            }
        }

        if (!string.IsNullOrEmpty(sapBaseResponse?.Error?.Message?.Value))
            await approvalService.FailedAsync(request.Id, sapBaseResponse.Error.Message.Value);

        return sapBaseResponse;
    }

    public async Task FinalizeApprovalAsync(ApprovalRequest result, ApprovalActionData? data, SapBaseResponse? sapResponse, CancellationToken cancellationToken = default)
    {
        if (sapResponse is not null)
        {
            result.SapResponseDocEntry = sapResponse.ApprovalDocEntry;
            result.SapResponseDocNum = sapResponse.ApprovalDocNumber;

            await unitOfWork.ExecuteInTransactionAsync(async _ =>
            {
                if (result.DocumentType == ApprovalDocumentType.Payments)
                {
                    var record = await FindStageWisePaymentForApprovalAsync(result);
                    if (record != null)
                    {
                        record.UtrDate = data?.UtrDate;
                        record.UtrNo = data?.UtrNo;
                        context.StageWisePayments.Update(record);
                    }
                }

                if (result.DocumentType is ApprovalDocumentType.Payments or ApprovalDocumentType.StagewisePayments_DP)
                    await stageWisePaymentService.MarkApprovedWhenAllRequestsCompleteAsync(result.Id);

                context.ApprovalRequests.Update(result);
            }, cancellationToken);
        }
    }

    private Task<StageWisePayment?> FindStageWisePaymentForApprovalAsync(ApprovalRequest request) =>
        GetStageWisePaymentLinkedToApprovalAsync(request.Id, request.CompanyDb, CancellationToken.None);

    /// <summary>
    /// Finds the (single) PendingApproval StageWisePayment row linked to a given approval request.
    /// Matches strictly on the ApprovalRequestId link column (set at request creation time), not on
    /// DocNumber/SupportingData — SupportingData is stored as the PO DocEntry while StageWisePayment.DocNumber
    /// is the PO DocNum, so comparing the two would silently miss records whenever DocEntry != DocNum.
    /// </summary>
    private async Task<StageWisePayment?> GetStageWisePaymentLinkedToApprovalAsync(int approvalRequestId, string companyDb, CancellationToken cancellationToken)
    {
        var records = await GetStageWisePaymentsLinkedToApprovalAsync(approvalRequestId, companyDb, cancellationToken);
        return records.FirstOrDefault();
    }

    private async Task<List<StageWisePayment>> GetStageWisePaymentsLinkedToApprovalAsync(int approvalRequestId, string companyDb, CancellationToken cancellationToken)
    {
        var idText = approvalRequestId.ToString();
        var candidates = await context.StageWisePayments
            .Where(x => x.CompanyDb == companyDb
                && x.Status == StageWisePaymentStatus.PendingApproval
                && x.ApprovalRequestId != null
                && x.ApprovalRequestId.Contains(idText))
            .ToListAsync(cancellationToken);

        return candidates.Where(x => IsLinkedToApprovalRequest(x, idText)).ToList();
    }

    private static bool IsLinkedToApprovalRequest(StageWisePayment record, string approvalRequestId) =>
        record.ApprovalRequestId?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(x => x == approvalRequestId) == true;
}
