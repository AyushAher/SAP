using System.Text.Json;
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
    ApprovalService approvalService,
    PurchaseOrders.PurchaseOrderLinkResolver purchaseOrderLinks)
{
    private static readonly JsonSerializerOptions RequestBodyJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<SapBaseResponse?> ExecuteAsync(ApprovalRequest request, ApprovalActionData? data, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.RequestBody))
        {
            await approvalService.FailedAsync(
                request.Id,
                "Unable to post to SAP — the stored approval payload is missing.");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.SapResponseDocEntry))
        {
            throw new ApiErrorException(
                BaseErrorCodes.Conflict,
                "A SAP document already exists for this approval request.");
        }

        try
        {
            var response = await ExecuteInternalAsync(request, data, cancellationToken);
            return await EnsurePaymentSapDocOrFailAsync(request, response);
        }
        catch (ApiErrorException ex)
        {
            // Final approval already mutated DB state — mark request Failed so it is visible
            // instead of leaving "Approved" with no SAP document.
            await approvalService.FailedAsync(request.Id, ex.Message);
            return new SapBaseResponse
            {
                Error = new SapError
                {
                    Message = new SapMessage { Value = ex.Message },
                },
            };
        }
        catch (Exception ex)
        {
            // Json/deserialization and unexpected SAP client failures must not leave the request
            // stuck as Approved with a blank SapResponseDocEntry (no Retry surface).
            var message = string.IsNullOrWhiteSpace(ex.Message)
                ? "SAP posting failed unexpectedly. Use Retry SAP."
                : ex.Message;
            await approvalService.FailedAsync(request.Id, message);
            return new SapBaseResponse
            {
                Error = new SapError
                {
                    Message = new SapMessage { Value = message },
                },
            };
        }
    }

    /// <summary>
    /// Payment / down-payment approvals must produce a SAP DocEntry. An empty success response used to
    /// leave OverallStatus=Approved and the StageWisePayment stuck on PendingApproval with no Retry cue.
    /// </summary>
    private async Task<SapBaseResponse?> EnsurePaymentSapDocOrFailAsync(ApprovalRequest request, SapBaseResponse? response)
    {
        var isPaymentApproval = request.DocumentType is ApprovalDocumentType.Payments
            or ApprovalDocumentType.StagewisePayments_DP;
        if (!isPaymentApproval)
            return response;

        if (!string.IsNullOrEmpty(response?.Error?.Message?.Value))
            return response;

        if (!string.IsNullOrWhiteSpace(response?.ApprovalDocEntry))
            return response;

        const string message = "SAP posting did not return a document entry. Use Retry SAP.";
        await approvalService.FailedAsync(request.Id, message);
        return new SapBaseResponse
        {
            Error = new SapError
            {
                Message = new SapMessage { Value = message },
            },
        };
    }

    private async Task<SapBaseResponse?> ExecuteInternalAsync(ApprovalRequest request, ApprovalActionData? data, CancellationToken cancellationToken)
    {
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
                if (sapBaseResponse is SapPurchaseOrdersResponse poResponse
                    && string.IsNullOrEmpty(poResponse.Error?.Message?.Value))
                {
                    sapBaseResponse.ApprovalDocEntry = poResponse.DocEntry?.ToString();
                    sapBaseResponse.ApprovalDocNumber = poResponse.DocNum?.ToString();
                }
                break;
            }
            case ApprovalDocumentType.StagewisePayments_DP:
            {
                var body = JsonSerializer.Deserialize<SapPurchaseDownPaymentRequest>(
                    request.RequestBody, RequestBodyJsonOptions);
                if (body is null)
                {
                    throw new ApiErrorException(
                        BaseErrorCodes.ValidationFailed,
                        "Unable to post to SAP — the down payment approval payload could not be read.");
                }

                var dpRecords = await GetStageWisePaymentsLinkedToApprovalAsync(
                    request.Id, request.CompanyDb, cancellationToken);
                var dpRecord = dpRecords.FirstOrDefault();
                StageWisePaymentBatch? dpBatch = null;
                if (dpRecord is not null)
                {
                    dpBatch = await context.StageWisePaymentBatches
                        .AsNoTracking()
                        .FirstOrDefaultAsync(b =>
                            b.ApprovalRequestId == request.Id.ToString()
                            || b.StageWisePaymentId == dpRecord.Id
                            || b.DownPaymentStageWisePaymentId == dpRecord.Id,
                            cancellationToken);
                }

                var postingDate = dpBatch?.PostingDate ?? body.DocDate ?? utrDate;
                var paymentDate = dpBatch?.PaymentDate ?? utrDate ?? postingDate;
                StageWisePaymentService.ApplyPostingDate(body, postingDate, paymentDate);
                if (utrDate is not null) body.DocDueDate = utrDate.Value;
                if (dpRecord is not null)
                    body.PaymentRequestId = StageWisePaymentService.FormatPaymentRequestId(dpRecord.Id);

                var dpUserRemark = !string.IsNullOrWhiteSpace(dpBatch?.JournalRemark)
                    ? dpBatch.JournalRemark.Trim()
                    : comment?.Trim();
                if (!string.IsNullOrWhiteSpace(dpUserRemark))
                    body.JournalMemo = dpUserRemark;
                else if (string.IsNullOrWhiteSpace(body.JournalMemo) && !string.IsNullOrWhiteSpace(body.Comments))
                    body.JournalMemo = body.Comments;

                if (request.Action == ApprovalAction.Create)
                {
                    var dpResponse = await sapPurchaseDownPaymentService.SaveDownPayment(
                        body, request.Id, request.SupportingData, ignoreApproval: true);
                    sapBaseResponse = dpResponse;

                    if (dpResponse?.PendingApproval == true)
                    {
                        throw new ApiErrorException(
                            BaseErrorCodes.Conflict,
                            "Down payment is already approved but SAP posting did not proceed. Use Retry SAP.");
                    }

                    var docEntry = dpResponse?.DocEntry?.ToString() ?? "";
                    var docNumber = dpResponse?.DocNum?.ToString() ?? "";

                    if (string.IsNullOrEmpty(sapBaseResponse?.Error?.Message?.Value)
                        && !string.IsNullOrWhiteSpace(docEntry))
                    {
                        foreach (var item in dpRecords)
                        {
                            if (string.IsNullOrEmpty(item.ApDownPaymentInvoiceEntryNumber))
                                item.ApDownPaymentInvoiceEntryNumber = dpResponse?.DocNum?.ToString();
                            else
                                item.ApDownPaymentInvoiceEntryNumber += "," + dpResponse?.DocNum;
                            item.DownPaymentDocEntry = AppendDocEntry(item.DownPaymentDocEntry, docEntry);
                            context.AttachModified(item);
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
                var body = JsonSerializer.Deserialize<SapVendorPaymentRequests>(
                    request.RequestBody, RequestBodyJsonOptions);
                if (body is null)
                {
                    throw new ApiErrorException(
                        BaseErrorCodes.ValidationFailed,
                        "Unable to post to SAP — the payment approval payload could not be read.");
                }

                body.CashFlowAssignments ??= [];
                body.PaymentInvoices ??= [];

                var record = await FindStageWisePaymentForApprovalAsync(request);
                var linkedRecords = await GetStageWisePaymentsLinkedToApprovalAsync(
                    request.Id, request.CompanyDb, cancellationToken);
                if (!string.IsNullOrWhiteSpace(record?.PaymentDocEntry)
                    || linkedRecords.Any(r => !string.IsNullOrWhiteSpace(r.PaymentDocEntry)))
                {
                    var existingDocEntry = record?.PaymentDocEntry
                        ?? linkedRecords.First(r => !string.IsNullOrWhiteSpace(r.PaymentDocEntry)).PaymentDocEntry;
                    return new SapBaseResponse
                    {
                        ApprovalDocEntry = existingDocEntry,
                        ApprovalDocNumber = record?.ApDownPaymentInvoiceEntryNumber
                            ?? linkedRecords.FirstOrDefault()?.ApDownPaymentInvoiceEntryNumber,
                    };
                }

                if (record is not null)
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
                    StageWisePaymentService.ApplyVendorPaymentDates(body, paymentDate, postingDate);
                    var remarks = Constants.PaymentRemarks.Build(
                        userRemark, body.BPLId, body.PoNumber);
                    body.Remarks = remarks;
                    body.JournalRemarks = remarks;

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

                if (request.Action == ApprovalAction.Create)
                {
                    var dpResponse = await sapVendorPaymentService.CreateVendorPayments(
                        body,
                        request.Id,
                        request.SupportingData,
                        ignoreApproval: true);
                    sapBaseResponse = dpResponse;

                    if (dpResponse?.PendingApproval == true)
                    {
                        throw new ApiErrorException(
                            BaseErrorCodes.Conflict,
                            "Payment is already approved but SAP posting did not proceed. Use Retry SAP.");
                    }

                    var docEntry = dpResponse?.DocEntry?.ToString() ?? "";
                    var docNumber = dpResponse?.DocNumber?.ToString() ?? "";

                    if (string.IsNullOrEmpty(sapBaseResponse?.Error?.Message?.Value)
                        && !string.IsNullOrWhiteSpace(docEntry))
                    {
                        var records = await GetStageWisePaymentsLinkedToApprovalAsync(request.Id, request.CompanyDb, cancellationToken);

                        foreach (var item in records)
                        {
                            if (string.IsNullOrEmpty(item.ApDownPaymentInvoiceEntryNumber))
                                item.ApDownPaymentInvoiceEntryNumber = dpResponse?.DocNumber?.ToString();
                            else
                                item.ApDownPaymentInvoiceEntryNumber += "," + dpResponse?.DocNumber;
                            item.PaymentDocEntry = docEntry;
                            context.AttachModified(item);
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
        if (sapResponse is not null
            && !string.IsNullOrWhiteSpace(sapResponse.ApprovalDocEntry))
        {
            result.SapResponseDocEntry = sapResponse.ApprovalDocEntry;
            result.SapResponseDocNum = sapResponse.ApprovalDocNumber;
        }

        if (result.PurchaseOrderId is null
            && sapResponse is not null
            && result.DocumentType is ApprovalDocumentType.PurchaseOrder
                or ApprovalDocumentType.StagewisePayments_DP
                or ApprovalDocumentType.Payments)
        {
            if (int.TryParse(sapResponse.ApprovalDocEntry, out var poDocEntry) && poDocEntry > 0
                && result.DocumentType == ApprovalDocumentType.PurchaseOrder)
            {
                result.PurchaseOrderId = await purchaseOrderLinks.EnsureIdByDocEntryAsync(poDocEntry, cancellationToken);
            }
            else if (int.TryParse(result.SupportingData, out var supportDocEntry) && supportDocEntry > 0)
            {
                result.PurchaseOrderId = await purchaseOrderLinks.EnsureIdByDocEntryAsync(supportDocEntry, cancellationToken);
            }
        }

        var isPaymentApproval = result.DocumentType is ApprovalDocumentType.Payments
            or ApprovalDocumentType.StagewisePayments_DP;
        if (!isPaymentApproval && sapResponse is null)
            return;

        await unitOfWork.ExecuteInTransactionAsync(async _ =>
        {
            if (result.DocumentType == ApprovalDocumentType.Payments)
            {
                var record = await FindStageWisePaymentForApprovalAsync(result);
                if (record != null)
                {
                    record.UtrDate = data?.UtrDate;
                    record.UtrNo = data?.UtrNo;
                    if (record.PurchaseOrderId is null && result.PurchaseOrderId is not null)
                        record.PurchaseOrderId = result.PurchaseOrderId;
                    context.AttachModified(record);
                }
            }

            if (isPaymentApproval)
            {
                await stageWisePaymentService.MarkApprovedWhenAllRequestsCompleteAsync(
                    result.Id, result.CompanyDb);
            }

            if (sapResponse is not null && !string.IsNullOrWhiteSpace(sapResponse.ApprovalDocEntry))
                context.AttachModified(result);
        }, cancellationToken);
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

    private static string AppendDocEntry(string? existing, string next) =>
        string.IsNullOrWhiteSpace(existing) ? next : existing + "," + next;
}
