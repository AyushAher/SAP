using System.Text.Json;
using SapApi.Domain.Entities;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Enums;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services;

public class ApprovalExecutionService(
    AppDbContext context,
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
                    ApprovalAction.Create => await sapPurchaseOrderService.CreatePurchaseOrder(body),
                    ApprovalAction.Update => await sapPurchaseOrderService.UpdatePurchaseOrder(body),
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
                        var records = await context.StageWisePayments
                            .Where(x => x.ApprovalRequestId != null && x.Status == StageWisePaymentStatus.PendingApproval
                                && x.DocNumber != null && x.DocNumber.ToString() == request.SupportingData)
                            .ToListAsync(cancellationToken);

                        foreach (var item in records)
                        {
                            if (!IsLinkedToApprovalRequest(item, request.Id.ToString())) continue;
                            if (string.IsNullOrEmpty(item.ApDownPaymentInvoiceEntryNumber))
                                item.ApDownPaymentInvoiceEntryNumber = dpResponse?.DocNum?.ToString();
                            else
                                item.ApDownPaymentInvoiceEntryNumber += "," + dpResponse?.DocNum;
                            context.StageWisePayments.Update(item);
                        }
                        await context.SaveChangesAsync(cancellationToken);
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
                    body.TransferReference = utrNo ?? "";
                    body.CounterReference = utrNo ?? "";
                    body.TransferDate = utrDate ?? DateTime.Now;
                    body.DocDueDate = utrDate ?? DateTime.Now;
                    body.DocDate = utrDate ?? DateTime.Now;
                    body.PostingDate = utrDate ?? DateTime.Now;
                    body.Remarks = comment;
                    body.JournalRemarks = comment;
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
                        var records = await context.StageWisePayments
                            .Where(x => x.ApprovalRequestId != null && x.Status == StageWisePaymentStatus.PendingApproval)
                            .ToListAsync(cancellationToken);

                        foreach (var item in records)
                        {
                            if (item.DocNumber?.ToString() != request.SupportingData) continue;
                            if (!IsLinkedToApprovalRequest(item, request.Id.ToString())) continue;

                            if (string.IsNullOrEmpty(item.ApDownPaymentInvoiceEntryNumber))
                                item.ApDownPaymentInvoiceEntryNumber = dpResponse?.DocNumber?.ToString();
                            else
                                item.ApDownPaymentInvoiceEntryNumber += "," + dpResponse?.DocNumber;
                            context.StageWisePayments.Update(item);
                        }
                        await context.SaveChangesAsync(cancellationToken);
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
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<StageWisePayment?> FindStageWisePaymentForApprovalAsync(ApprovalRequest request)
    {
        var records = await context.StageWisePayments
            .Where(x => x.ApprovalRequestId != null && request.SupportingData == x.DocNumber.ToString())
            .ToListAsync();
        return records.FirstOrDefault(x => IsLinkedToApprovalRequest(x, request.Id.ToString()));
    }

    private static bool IsLinkedToApprovalRequest(StageWisePayment record, string approvalRequestId) =>
        record.ApprovalRequestId?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(x => x == approvalRequestId) == true;
}
