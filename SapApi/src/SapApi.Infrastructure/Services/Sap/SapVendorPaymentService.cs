using SapApi.Shared.Enums;
﻿using SapApi.Domain.Interfaces;
using SapApi.Shared;
using SapApi.Domain.Entities;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using Serilog;

namespace SapApi.Infrastructure.Services.Sap
{
    public class SapVendorPaymentService(IHttpRequestHandler httpRequestHandler, ApprovalService approvalService)
    {
        private const string ApInvoiceListSelect =
            "CardCode,DocEntry,DocNum,WTAmount,DocTotal,NumAtCard,PaidToDate,DocumentStatus,DocumentLines";

        private const string GrpoListSelect = "CardCode,DocumentLines,DocEntry,DocNum";

        private const int PurchaseOrderBaseType = 22;
        private const int GrpoBaseType = 20;

        /// <summary>
        /// SAP cannot filter on DocumentLines (no lambda / path filters on the line collection), so
        /// documents are matched to a purchase order in this service. Requesting a larger page than
        /// the Service Layer default (20) keeps that walk to a couple of round trips; the page cap
        /// bounds both the call count and the memory held per lookup.
        /// </summary>
        private const int LookupPageSize = 100;
        private const int MaxLookupPages = 20;

        public async Task<SapVendorPaymentsResponse?> CreateVendorPayments(SapVendorPaymentRequests requests, int? reqId = null, string? supportingData = null, bool ignoreApproval = false)
        {
            if (!ignoreApproval)
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

        public Task<GetAllSapPurchaseInvoicesResponse?> GetApInvoicesForPurchaseOrder(string cardCode, int poDocEntry) =>
            GetApInvoicesForPurchaseOrder(cardCode, poDocEntry, []);

        /// <summary>
        /// Every open AP invoice of the vendor that is copied from the purchase order, either directly
        /// or through one of <paramref name="grpoDocEntries"/>. A purchase order can be invoiced both
        /// ways, so both link types are collected in one walk of the vendor's invoices.
        /// </summary>
        public Task<GetAllSapPurchaseInvoicesResponse?> GetApInvoicesForPurchaseOrder(
            string cardCode,
            int poDocEntry,
            IReadOnlyCollection<int> grpoDocEntries)
        {
            var grpoSet = grpoDocEntries.ToHashSet();

            return CollectLinkedDocumentsAsync(
                Constants.SapApiUrls.GetAllPurchaseInvoices,
                ApInvoiceListSelect,
                $"CardCode eq '{EscapeODataValue(cardCode)}' and DocumentStatus eq 'bost_Open'",
                document => HasBaseLine(document, PurchaseOrderBaseType, poDocEntry)
                    || (grpoSet.Count > 0 && HasBaseLine(document, GrpoBaseType, grpoSet)),
                "AP invoices",
                cardCode);
        }

        public Task<SapPurchaseInvoicesResponse?> GetApInvoiceByDocEntry(string cardCode, int docEntry)
        {
            var sapQueries = new SapQueries { Select = ApInvoiceListSelect };
            return httpRequestHandler.GetAsync<SapPurchaseInvoicesResponse>(
                Constants.SapApiUrls.GetAllPurchaseInvoices + $"({docEntry})" + sapQueries.GetQueryValue());
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

        /// <summary>
        /// Goods receipt POs copied from the purchase order. Partly invoiced receipts are still open,
        /// so receipt status is not filtered — the AP invoices raised against them must stay reachable.
        /// </summary>
        public Task<GetAllSapPurchaseInvoicesResponse?> GetGrposForPurchaseOrder(string cardCode, int poDocEntry) =>
            CollectLinkedDocumentsAsync(
                Constants.SapApiUrls.GetAllPurchaseDeliveryNotes,
                GrpoListSelect,
                $"CardCode eq '{EscapeODataValue(cardCode)}'",
                document => HasBaseLine(document, PurchaseOrderBaseType, poDocEntry),
                "goods receipt POs",
                cardCode);

        /// <summary>
        /// Walks a vendor's documents page by page and keeps only the ones linked to the purchase
        /// order. Only the matches are retained across pages, so memory stays proportional to the
        /// result rather than to the vendor's document history.
        /// </summary>
        private async Task<GetAllSapPurchaseInvoicesResponse?> CollectLinkedDocumentsAsync(
            string collectionUrl,
            string select,
            string filter,
            Func<SapPurchaseInvoicesResponse, bool> isLinked,
            string documentLabel,
            string cardCode)
        {
            var matches = new List<SapPurchaseInvoicesResponse>();

            for (var page = 0; page < MaxLookupPages; page++)
            {
                var sapQueries = new SapQueries
                {
                    Select = select,
                    Filter = filter,
                    OrderBy = "DocEntry desc",
                    Skip = page == 0 ? null : (page * LookupPageSize).ToString(),
                };

                var response = await httpRequestHandler.GetPageAsync<GetAllSapPurchaseInvoicesResponse>(
                    collectionUrl + sapQueries.GetQueryValue(),
                    LookupPageSize);

                if (response?.Error is not null)
                    return response;

                var rows = response?.Value;
                if (rows is null || rows.Count == 0)
                    break;

                matches.AddRange(rows.Where(isLinked));

                if (!response!.HasNextPage || rows.Count < LookupPageSize)
                    break;

                if (page == MaxLookupPages - 1)
                {
                    Log.Warning(
                        "Stopped scanning {DocumentLabel} for vendor {CardCode} after {Pages} pages; "
                        + "documents beyond {Scanned} rows were not checked.",
                        documentLabel,
                        cardCode,
                        MaxLookupPages,
                        MaxLookupPages * LookupPageSize);
                }
            }

            return new GetAllSapPurchaseInvoicesResponse { Value = matches };
        }

        private static bool HasBaseLine(SapPurchaseInvoicesResponse document, int baseType, int baseEntry) =>
            document.DocumentLines?.Any(line => line.BaseType == baseType && line.BaseEntry == baseEntry) == true;

        private static bool HasBaseLine(SapPurchaseInvoicesResponse document, int baseType, HashSet<int> baseEntries) =>
            document.DocumentLines?.Any(line =>
                line.BaseType == baseType && line.BaseEntry.HasValue && baseEntries.Contains(line.BaseEntry.Value)) == true;

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
