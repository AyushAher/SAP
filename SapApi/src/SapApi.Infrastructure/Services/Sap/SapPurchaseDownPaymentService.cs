using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services.Sap;

public class SapPurchaseDownPaymentService(
    IHttpRequestHandler httpRequestHandler,
    ApprovalService approvalService,
    SapDocumentSeriesService documentSeriesService)
{
    public async Task<SapPurchaseDownPaymentResponse?> SaveDownPayment(
        SapPurchaseDownPaymentRequest request,
        int? reqId = null,
        string? supportingData = null,
        bool ignoreApproval = false,
        CancellationToken cancellationToken = default)
    {
        if (!ignoreApproval)
        {
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(
                reqId, request, ApprovalDocumentType.StagewisePayments_DP, ApprovalAction.Create, supportingData);
            if (policyApproval.PendingApproval)
            {
                return new SapPurchaseDownPaymentResponse
                {
                    PendingApproval = true,
                    PendingApprovalRequestId = policyApproval.PendingApprovalRequestId
                };
            }
        }

        // Resolve ODPO Series for BPL + DocDate FY before POST — bypasses broken user default series
        // ("To generate this document, first define the numbering series in the Administration module").
        await documentSeriesService.EnsurePurchaseDownPaymentSeriesAsync(request, cancellationToken);

        return await httpRequestHandler.PostAsync<SapPurchaseDownPaymentRequest, SapPurchaseDownPaymentResponse>(
            Constants.SapApiUrls.PurchaseDownPayment, request);
    }

    public async Task<SapBaseResponse?> CancelDownPayment(string docEntry)
    {
        return await httpRequestHandler.PostAsync<object, SapBaseResponse>(
            Constants.SapApiUrls.CancelPurchaseDownPayment(docEntry), null);
    }

    public async Task<GetAllSapPurchaseDownPaymentResponse?> GetPurchaseDownPaymentByDocNum(string docEntry)
    {
        return await httpRequestHandler.GetAsync<GetAllSapPurchaseDownPaymentResponse>(
            Constants.SapApiUrls.GetPurchaseDownPaymentByDocNum(docEntry));
    }
}
