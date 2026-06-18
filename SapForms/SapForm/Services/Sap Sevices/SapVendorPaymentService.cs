using SapForm.Services.Helpers;
using Shared;
using Shared.Entities;
using Shared.Requests;
using Shared.Responses.Sap;

namespace SapForm.Services
{
    public class SapVendorPaymentService(IHttpRequestHandler httpRequestHandler, ApprovalService approvalService)
    {
        public async Task<SapVendorPaymentsResponse?> CreateVendorPayments(SapVendorPaymentRequests requests, int? reqId = null, string? supportingData = null, bool? ignoreApproval = false)
        {
            if (ignoreApproval == false)
            {
                SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(reqId, requests, ApprovalDocumentType.Payments, ApprovalAction.Create, supportingData);
                if (policyApproval.PendingApproval)
                {

                    return new SapVendorPaymentsResponse
                    {
                        PendingApproval = true,
                        PendingApprovalRequestId = policyApproval.PendingApprovalRequestId
                    };
                }
            }
            SapVendorPaymentsResponse? data = await httpRequestHandler.PostAsync<SapVendorPaymentRequests, SapVendorPaymentsResponse>(
                Constants.SapApiUrls.CreateVendorPayments,
                requests);
            return data;
        }

        public async Task<GetAllSapPurchaseInvoicesResponse?> GetApInvoices(string cardCode)
        {
            var sapQueries = new SapQueries
            {
                Select = "CardCode,DocumentLines,DocEntry,DocNum,WTAmount,DocTotal,NumAtCard,PaidToDate,DocumentStatus,WTAmount",
                Filter = $"CardCode eq '{cardCode}' and DocumentStatus eq 'bost_Open'"
            };

            var data = await httpRequestHandler.GetAsync<GetAllSapPurchaseInvoicesResponse>(
                Constants.SapApiUrls.GetAllPurchaseInvoices + sapQueries.GetQueryValue());
            return data;
        }

        public async Task<GetAllSapPurchaseInvoicesResponse?> GetGrpo(string cardCode)
        {
            var sapQueries = new SapQueries
            {
                Select = "CardCode,DocumentLines,DocEntry,DocNum",
                Filter = $"CardCode eq '{cardCode}' and DocumentStatus eq 'bost_Close'"
            };

            var data = await httpRequestHandler.GetAsync<GetAllSapPurchaseInvoicesResponse>(
                Constants.SapApiUrls.GetAllPurchaseDeliveryNotes + sapQueries.GetQueryValue());
            return data;
        }

        public async Task<SapBaseResponse?> CancelVendorPayment(string docEntry)
        {
            var response = await httpRequestHandler.PostAsync<object, SapBaseResponse>(
                Constants.SapApiUrls.CancelVendorPayment(docEntry), null);
            return response;
        }
        public async Task<SapVendorPaymentsResponse?> GetVendorPayment(string docEntry)
        {
            var response = await httpRequestHandler.GetAsync<SapVendorPaymentsResponse>(
                Constants.SapApiUrls.GetVendorPayment(docEntry));
            return response;
        }

        public async Task<GetAllSapVendorPaymentsResponse?> GetVendorPaymentByDocEntry(string docEntry)
        {
            var response = await httpRequestHandler.GetAsync<GetAllSapVendorPaymentsResponse>(
                Constants.SapApiUrls.GetVendorPaymentByDocEntry(docEntry));
            return response;
        }
    }
}