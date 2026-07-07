using SapApi.Shared.Enums;
﻿using SapApi.Domain.Interfaces;
using SapApi.Shared;
using SapApi.Domain.Entities;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services.Sap
{
    public class SapVendorPaymentService(IHttpRequestHandler httpRequestHandler, ApprovalService approvalService)
    {
        private const string ApInvoiceListSelect =
            "CardCode,DocEntry,DocNum,WTAmount,DocTotal,NumAtCard,PaidToDate,DocumentStatus,DocumentLines";

        private const string GrpoListSelect = "CardCode,DocumentLines,DocEntry,DocNum";

        private const int PurchaseOrderBaseType = 22;
        private const int GrpoBaseType = 20;

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

        public Task<GetAllSapPurchaseInvoicesResponse?> GetApInvoices(string cardCode)
        {
            var sapQueries = new SapQueries
            {
                Select = ApInvoiceListSelect,
                Filter = $"CardCode eq '{EscapeODataValue(cardCode)}' and DocumentStatus eq 'bost_Open'"
            };

            return httpRequestHandler.GetAsync<GetAllSapPurchaseInvoicesResponse>(
                Constants.SapApiUrls.GetAllPurchaseInvoices + sapQueries.GetQueryValue());
        }

        public async Task<GetAllSapPurchaseInvoicesResponse?> GetApInvoicesForPurchaseOrder(string cardCode, int poDocEntry)
        {
            var response = await GetApInvoices(cardCode);
            if (response?.Error is not null)
                return response;

            return FilterInvoicesByBase(response, PurchaseOrderBaseType, poDocEntry);
        }

        public async Task<GetAllSapPurchaseInvoicesResponse?> GetApInvoicesForGrpos(string cardCode, IReadOnlyCollection<int> grpoDocEntries)
        {
            if (grpoDocEntries.Count == 0)
                return new GetAllSapPurchaseInvoicesResponse { Value = [] };

            var response = await GetApInvoices(cardCode);
            if (response?.Error is not null)
                return response;

            var grpoSet = grpoDocEntries.ToHashSet();
            return new GetAllSapPurchaseInvoicesResponse
            {
                Value = response?.Value?
                    .Where(x => x.DocumentLines?.Any(d =>
                        d.BaseType == GrpoBaseType && d.BaseEntry.HasValue && grpoSet.Contains(d.BaseEntry.Value)) == true)
                    .ToList() ?? [],
            };
        }

        public Task<SapPurchaseInvoicesResponse?> GetApInvoiceByDocEntry(string cardCode, int docEntry)
        {
            var sapQueries = new SapQueries { Select = ApInvoiceListSelect };
            return httpRequestHandler.GetAsync<SapPurchaseInvoicesResponse>(
                Constants.SapApiUrls.GetAllPurchaseInvoices + $"({docEntry})" + sapQueries.GetQueryValue(),
                checkCache: false);
        }

        public Task<GetAllSapPurchaseInvoicesResponse?> GetGrpo(string cardCode)
        {
            var sapQueries = new SapQueries
            {
                Select = GrpoListSelect,
                Filter = $"CardCode eq '{EscapeODataValue(cardCode)}' and DocumentStatus eq 'bost_Close'"
            };

            return httpRequestHandler.GetAsync<GetAllSapPurchaseInvoicesResponse>(
                Constants.SapApiUrls.GetAllPurchaseDeliveryNotes + sapQueries.GetQueryValue());
        }

        public async Task<GetAllSapPurchaseInvoicesResponse?> GetGrposForPurchaseOrder(string cardCode, int poDocEntry)
        {
            var sapQueries = new SapQueries
            {
                Select = GrpoListSelect,
                Filter = $"CardCode eq '{EscapeODataValue(cardCode)}' and DocumentStatus eq 'bost_Close'",
                OrderBy = "DocEntry desc",
                Top = "200",
            };

            var response = await httpRequestHandler.GetAsync<GetAllSapPurchaseInvoicesResponse>(
                Constants.SapApiUrls.GetAllPurchaseDeliveryNotes + sapQueries.GetQueryValue(),
                checkCache: false);

            return FilterInvoicesByBase(response, PurchaseOrderBaseType, poDocEntry);
        }

        private static GetAllSapPurchaseInvoicesResponse FilterInvoicesByBase(
            GetAllSapPurchaseInvoicesResponse? response,
            int baseType,
            int baseEntry)
        {
            return new GetAllSapPurchaseInvoicesResponse
            {
                Value = response?.Value?
                    .Where(x => x.DocumentLines?.Any(d =>
                        d.BaseType == baseType && d.BaseEntry == baseEntry) == true)
                    .ToList() ?? [],
            };
        }

        private static string EscapeODataValue(string value) => value.Replace("'", "''");

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
