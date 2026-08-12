using SapApi.Shared.Enums;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Sap;
using SapApi.Shared;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Infrastructure.Services.Sap
{
    public class SapProductionOrdersService(
        IHttpRequestHandler httpRequestHandler,
        ApprovalService approvalService,
        SapMasterDataService masterDataService)
    {
        /// <summary>Filter keys the UI sends for name columns that ProductionOrders itself does not expose.</summary>
        private static readonly string[] ProjectNameFilterFields = ["ProjectName", "U_PrjName"];
        private static readonly string[] CustomerNameFilterFields = ["CustomerName", "CardName", "U_CustomerName"];
        private const int NameFilterCodeLimit = 50;

        public async Task<GetAllSapProductionOrdersResponse?> GetAllProductionOrders()
        {
            var sapQueries = SapPaginationBuilder.ToSapQueries(
                new PaginationRequest { PageNumber = 1, PageSize = 20 },
                SapPaginationProfiles.ProductionOrders);

            return await GetAllProductionOrdersInternal(sapQueries);
        }

        public async Task<PaginationResponse<List<SapProductionOrdersResponse>>> GetAllProductionOrdersPaginated(
            PaginationRequest request,
            CancellationToken cancellationToken = default)
        {
            var profile = await ApplyNameFiltersAsync(request, cancellationToken);
            if (profile is null)
                return PaginationResponseFactory.Create(request, new List<SapProductionOrdersResponse>(), 0);

            var sapQueries = SapPaginationBuilder.ToSapQueries(request, profile);
            // GetOrThrowAsync: a swallowed SAP failure would present as an empty/short list and hide
            // Service Layer errors (wrong $filter field, session, etc.) from the UI.
            var response = await httpRequestHandler.GetOrThrowAsync<GetAllSapProductionOrdersResponse>(
                Constants.SapApiUrls.GetAllProductionOrders + sapQueries.GetQueryValue());
            var items = response?.Value ?? [];
            var totalCount = response is null
                ? 0
                : SapPaginationBuilder.ResolveTotalCount(response, items, request);

            await FillMasterNamesAsync(items, cancellationToken);

            return PaginationResponseFactory.Create(request, items, totalCount);
        }

        /// <summary>
        /// Turns project-name / business-partner-name filters into code filters on the real
        /// ProductionOrders fields (Project, CustomerCode) by first matching the typed keyword against
        /// master data. Returns null when nothing matches, so callers return an empty page rather than
        /// an unfiltered list.
        /// </summary>
        private async Task<SapPaginationOptions?> ApplyNameFiltersAsync(
            PaginationRequest request,
            CancellationToken cancellationToken)
        {
            // Names are not sortable in SAP for this document; drop such sorts instead of sending an
            // unknown $orderby field.
            request.Sorts = request.Sorts
                .Where(s => !ProjectNameFilterFields.Contains(s.Field, StringComparer.OrdinalIgnoreCase)
                            && !CustomerNameFilterFields.Contains(s.Field, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var projectTerms = TakeFilters(request, ProjectNameFilterFields);
            var customerTerms = TakeFilters(request, CustomerNameFilterFields);
            if (projectTerms.Count == 0 && customerTerms.Count == 0)
                return SapPaginationProfiles.ProductionOrders;

            var clauses = new List<string>();

            foreach (var term in projectTerms)
            {
                var page = await masterDataService.SearchProjectsAsync(
                    NameSearchPage(term),
                    cancellationToken);
                var codes = Codes(page.Data?.Select(p => p.ProjectCode));
                if (codes.Count == 0) return null;
                clauses.Add(OrEquals("Project", codes));
            }

            foreach (var term in customerTerms)
            {
                var page = await masterDataService.SearchCustomersAsync(
                    NameSearchPage(term),
                    cancellationToken);
                var codes = Codes(page.Data?.Select(bp => bp.CardCode));
                if (codes.Count == 0) return null;
                clauses.Add(OrEquals("CustomerCode", codes));
            }

            var baseProfile = SapPaginationProfiles.ProductionOrders;
            var extra = string.Join(" and ", clauses);
            return new SapPaginationOptions
            {
                BaseFilter = string.IsNullOrWhiteSpace(baseProfile.BaseFilter)
                    ? extra
                    : $"({baseProfile.BaseFilter}) and {extra}",
                Select = baseProfile.Select,
                KeyFields = baseProfile.KeyFields,
                FieldMap = baseProfile.FieldMap,
                DefaultSortField = baseProfile.DefaultSortField,
                DefaultSortDirection = baseProfile.DefaultSortDirection,
                SearchOrFields = baseProfile.SearchOrFields,
                SearchCodeFields = baseProfile.SearchCodeFields,
                NumericSearchCodeFields = baseProfile.NumericSearchCodeFields,
                SearchTextFields = baseProfile.SearchTextFields,
            };
        }

        /// <summary>Removes the matching filters from the request and returns their search terms.</summary>
        private static List<string> TakeFilters(PaginationRequest request, string[] fields)
        {
            var matched = request.Filters
                .Where(f => fields.Contains(f.Field, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (matched.Count == 0) return [];

            request.Filters = request.Filters.Except(matched).ToList();
            return matched
                .Select(f => f.Value?.ToString()?.Trim() ?? string.Empty)
                .Where(term => !string.IsNullOrEmpty(term))
                .ToList();
        }

        private static PaginationRequest NameSearchPage(string term) => new()
        {
            PageNumber = 1,
            PageSize = NameFilterCodeLimit,
            Filters = [new FilterModel { Field = "__search", Operator = "contains", Value = term }],
        };

        private static List<string> Codes(IEnumerable<string?>? values) =>
            (values ?? [])
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(NameFilterCodeLimit)
                .ToList();

        private static string OrEquals(string field, IReadOnlyList<string> codes) =>
            $"({string.Join(" or ", codes.Select(code => $"{field} eq '{SapPaginationBuilder.EscapeODataString(code)}'"))})";

        /// <summary>Fills ProjectName / CustomerName from cached master data (batched, one call per page).</summary>
        private async Task FillMasterNamesAsync(
            List<SapProductionOrdersResponse> items,
            CancellationToken cancellationToken)
        {
            if (items.Count == 0) return;

            var projectCodes = items
                .Select(x => x.Project)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var cardCodes = items
                .Select(x => x.CustomerCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (projectCodes.Count == 0 && cardCodes.Count == 0) return;

            var lookup = await masterDataService.LookupMasterDataAsync(
                new MasterLookupRequest { ProjectCodes = projectCodes, CardCodes = cardCodes },
                cancellationToken);

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.ProjectName)
                    && !string.IsNullOrWhiteSpace(item.Project)
                    && lookup.Projects.TryGetValue(item.Project, out var projectName))
                    item.ProjectName = projectName;

                if (string.IsNullOrWhiteSpace(item.CustomerName)
                    && !string.IsNullOrWhiteSpace(item.CustomerCode)
                    && lookup.BusinessPartners.TryGetValue(item.CustomerCode, out var cardName))
                    item.CustomerName = cardName;
            }
        }

        private Task<GetAllSapProductionOrdersResponse?> GetAllProductionOrdersInternal(SapQueries sapQueries) =>
            httpRequestHandler.GetOrThrowAsync<GetAllSapProductionOrdersResponse>(
                Constants.SapApiUrls.GetAllProductionOrders + sapQueries.GetQueryValue());


        public async Task<GetAllSapProductionOrderLinesResponse?> GetProductionOrderLines(string docEntry)
        {
            return await httpRequestHandler.ExecuteSqlQueryAsync<GetAllSapProductionOrderLinesResponse>(Constants.SapSqlQueryName
                .GetProductionOrderLines, new Dictionary<string, object>
                {
                    { "_docentry", docEntry }
                });
        }

        public async Task<SapProductionOrdersResponse?> GetProductionOrders(string id, bool checkCache = false)
        {
            _ = checkCache;
            return await httpRequestHandler.GetAsync<SapProductionOrdersResponse>(Constants.SapApiUrls
                .GetProductionOrders(id));
        }

        public async Task<SapProductionOrdersResponse?> UpdateProductionOrderAsync(SapProductionOrdersResponse addedLines, int? policyRequestId = null)
        {
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, addedLines, ApprovalDocumentType.ProductionOrder, ApprovalAction.Update);
            if (policyApproval.PendingApproval)
            {
                return new SapProductionOrdersResponse
                {
                    PendingApproval = true,
                    PendingApprovalRequestId = policyApproval.PendingApprovalRequestId,
                };
            }

            var payload = PrepareProductionOrderForSapPut(addedLines);
            return await httpRequestHandler.PutAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                Constants.SapApiUrls.GetProductionOrders(payload.AbsoluteEntry?.ToString() ?? "0"), payload);
        }

        static SapProductionOrdersResponse PrepareProductionOrderForSapPut(SapProductionOrdersResponse order)
        {
            order.ProductionOrderLines = order.ProductionOrderLines?
                .Select((line, index) =>
                {
                    line.VisualOrder = index;
                    line.DocumentAbsoluteEntry = order.AbsoluteEntry;
                    line.SerialNumbers = null;
                    line.BatchNumbers = null;
                    // ProductionOrderLine.UoMCode must be a whole number (UoM entry). Drop inventory UoM names like "KG".
                    line.UoMCode = SapProductionOrderUoMNormalizer.NormalizeUoMCode(line.UoMCode);
                    return line;
                })
                .ToList() ?? [];

            // Project/customer names are display-only values resolved from master data. They map to UDFs
            // that do not exist on ProductionOrders in every company DB, and SAP rejects unknown
            // properties outright ("Property 'U_CustomerName' of 'ProductionOrder' is invalid").
            order.ProjectName = null;
            order.CustomerName = null;

            order.ProductionOrdersSalesOrderLines = null;
            order.ProductionOrdersStages = null;
            order.ProductionOrdersDocumentReferences = null;
            order.ODataMetadata = null;
            order.ODataNextLink = null;
            order.Error = null;

            return order;
        }

        public async Task<SapProductionOrdersResponse?> CreateProductionOrderAsync(SapProductionOrdersResponse addedLines, int? policyRequestId = null)
        {
            SapBaseResponse policyApproval = await approvalService.CheckApprovalPolicy(policyRequestId, addedLines, ApprovalDocumentType.ProductionOrder, ApprovalAction.Create);
            if (policyApproval.PendingApproval)
            {
                return new SapProductionOrdersResponse
                {
                    PendingApproval = true,
                    PendingApprovalRequestId = policyApproval.PendingApprovalRequestId,
                };
            }
            return await httpRequestHandler.PostAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                Constants.SapApiUrls.CreateProductionOrder, addedLines);
        }
    }
}
