using SapApi.Shared.Enums;
﻿using SapApi.Domain.Interfaces;
using SapApi.Shared;
using SapApi.Domain.Entities;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services.Sap
{
    public class SapPurchaseDownPaymentService(IHttpRequestHandler httpRequestHandler, ApprovalService approvalService)
    {
        public async Task<SapPurchaseDownPaymentResponse?> SaveDownPayment(SapPurchaseDownPaymentRequest request, int? reqId = null, string? supportingData = null)
        {
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(reqId, request, ApprovalDocumentType.StagewisePayments_DP, ApprovalAction.Create, supportingData);
            if (policyApproval.PendingApproval)
            {

                return new SapPurchaseDownPaymentResponse
                {
                    PendingApproval = true,
                    PendingApprovalRequestId = policyApproval.PendingApprovalRequestId
                };
            }
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
}