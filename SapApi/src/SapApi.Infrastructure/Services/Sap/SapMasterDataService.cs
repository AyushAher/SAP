using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Sap;
using SapApi.Shared;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Infrastructure.Services.Sap;

public class SapMasterDataService(IHttpRequestHandler http, ISapLoginService sapLogin)
{
    private const int LookupBatchSize = 20;

    public Task<PaginationResponse<List<ItemsResponse>>> SearchItemsAsync(PaginationRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync<SapItemsResponse, ItemsResponse>(
            Constants.SapApiUrls.ItemsCollection,
            SapPaginationProfiles.Items,
            request,
            r => r?.Value,
            cancellationToken);

    public Task<PaginationResponse<List<WarehouseResponse>>> SearchWarehousesAsync(PaginationRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync<SapWarehousesResponse, WarehouseResponse>(
            Constants.SapApiUrls.WarehousesCollection,
            SapPaginationProfiles.Warehouses,
            request,
            r => r?.Value,
            cancellationToken);

    public Task<PaginationResponse<List<SapTaxCodesResponse>>> SearchTaxCodesAsync(PaginationRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync<GetAllSapTaxCodesResponse, SapTaxCodesResponse>(
            Constants.SapApiUrls.SalesTaxCodesCollection,
            SapPaginationProfiles.TaxCodes,
            request,
            r => r?.Value,
            cancellationToken);

    public Task<PaginationResponse<List<SapProjectDetailsResponse>>> SearchProjectsAsync(PaginationRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync<SapGetAllProjectDetailsResponse, SapProjectDetailsResponse>(
            Constants.SapApiUrls.ProjectsCollection,
            SapPaginationProfiles.Projects,
            request,
            r => r?.Value,
            cancellationToken);

    public Task<PaginationResponse<List<SapBranchesResponse>>> SearchBusinessPlacesAsync(PaginationRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync<SapGetAllBranchesResponse, SapBranchesResponse>(
            Constants.SapApiUrls.BusinessPlacesCollection,
            SapPaginationProfiles.BusinessPlaces,
            request,
            r => r?.Value,
            cancellationToken);

    public Task<PaginationResponse<List<SapBusinessPartner>>> SearchVendorsAsync(PaginationRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync<SapBusinessPartnerResponse, SapBusinessPartner>(
            Constants.SapApiUrls.BusinessPartnersCollection,
            SapPaginationProfiles.Vendors,
            request,
            r => r?.Value,
            cancellationToken);

    public Task<PaginationResponse<List<SapBusinessPartner>>> SearchCustomersAsync(PaginationRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync<SapBusinessPartnerResponse, SapBusinessPartner>(
            Constants.SapApiUrls.BusinessPartnersCollection,
            SapPaginationProfiles.Customers,
            request,
            r => r?.Value,
            cancellationToken);

    public Task<PaginationResponse<List<SapSalesOrderResponse>>> SearchSalesOrdersAsync(
        PaginationRequest request,
        string? customerId = null,
        CancellationToken cancellationToken = default) =>
        SearchAsync<GetAllSapSalesOrderResponse, SapSalesOrderResponse>(
            Constants.SapApiUrls.OrdersCollection,
            SapPaginationProfiles.SalesOrders(customerId),
            request,
            r => r?.Value,
            cancellationToken);

    public async Task<SapBusinessPartner?> GetBusinessPartnerByCardCodeAsync(string cardCode, CancellationToken cancellationToken = default)
    {
        await sapLogin.SapLoginAsync(cancellationToken);
        var safeCode = SapPaginationBuilder.EscapeODataString(cardCode);
        var queries = new SapQueries
        {
            Filter = $"CardCode eq '{safeCode}'",
            Select = "CardCode,CardName,CardType",
            Top = "1",
        };
        var response = await http.GetAsync<SapBusinessPartnerResponse>(
            Constants.SapApiUrls.BusinessPartnersCollection + queries.GetQueryValue(),
            checkCache: true,
            cancellationToken: cancellationToken);
        return response?.Value?.FirstOrDefault();
    }

    public async Task<ItemsResponse?> GetItemByCodeAsync(string itemCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemCode))
            return null;

        await sapLogin.SapLoginAsync(cancellationToken);
        var safeCode = SapPaginationBuilder.EscapeODataString(itemCode);
        var queries = new SapQueries
        {
            Filter = $"ItemCode eq '{safeCode}'",
            Select = "ItemCode,ItemName,ItemsGroupCode,InventoryItem,InventoryUOM",
            Top = "1",
        };
        var response = await http.GetAsync<SapItemsResponse>(
            Constants.SapApiUrls.ItemsCollection + queries.GetQueryValue(),
            checkCache: true,
            cancellationToken: cancellationToken);
        return response?.Value?.FirstOrDefault();
    }

    public async Task<WarehouseResponse?> GetWarehouseByCodeAsync(string? warehouseCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(warehouseCode))
            return null;

        await sapLogin.SapLoginAsync(cancellationToken);
        var safeCode = SapPaginationBuilder.EscapeODataString(warehouseCode);
        var queries = new SapQueries
        {
            Filter = $"WarehouseCode eq '{safeCode}'",
            Select = "WarehouseCode,State,City,Location",
            Top = "1",
        };
        var response = await http.GetAsync<SapWarehousesResponse>(
            Constants.SapApiUrls.WarehousesCollection + queries.GetQueryValue(),
            checkCache: true,
            cancellationToken: cancellationToken);
        return response?.Value?.FirstOrDefault();
    }

    public async Task<SapProjectDetailsResponse?> GetProjectByCodeAsync(string? projectCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectCode))
            return null;

        await sapLogin.SapLoginAsync(cancellationToken);
        var safeCode = SapPaginationBuilder.EscapeODataString(projectCode);
        var queries = new SapQueries
        {
            Filter = $"Code eq '{safeCode}'",
            Select = "Code,Name",
            Top = "1",
        };
        var response = await http.GetAsync<SapGetAllProjectDetailsResponse>(
            Constants.SapApiUrls.ProjectsCollection + queries.GetQueryValue(),
            checkCache: true,
            cancellationToken: cancellationToken);
        return response?.Value?.FirstOrDefault();
    }

    public async Task<SapBranchesResponse?> GetBusinessPlaceByIdAsync(int? bplId, CancellationToken cancellationToken = default)
    {
        if (bplId is null)
            return null;

        await sapLogin.SapLoginAsync(cancellationToken);
        var queries = new SapQueries
        {
            Filter = $"BPLID eq {bplId.Value}",
            Select = "BPLID,BPLName,Address",
            Top = "1",
        };
        var response = await http.GetAsync<SapGetAllBranchesResponse>(
            Constants.SapApiUrls.BusinessPlacesCollection + queries.GetQueryValue(),
            checkCache: true,
            cancellationToken: cancellationToken);
        return response?.Value?.FirstOrDefault();
    }

    public async Task<string?> GetProjectNameAsync(string? projectCode, CancellationToken cancellationToken = default)
    {
        var project = await GetProjectByCodeAsync(projectCode, cancellationToken);
        return project?.ProjectName;
    }

    public async Task<MasterLookupResponse> LookupMasterDataAsync(
        MasterLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        var itemCodes = request.ItemCodes.Where(static c => !string.IsNullOrWhiteSpace(c)).Select(static c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var projectCodes = request.ProjectCodes.Where(static c => !string.IsNullOrWhiteSpace(c)).Select(static c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var cardCodes = request.CardCodes.Where(static c => !string.IsNullOrWhiteSpace(c)).Select(static c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        await sapLogin.SapLoginAsync(cancellationToken);

        var itemsTask = LookupItemsBatchAsync(itemCodes, cancellationToken);
        var projectsTask = LookupProjectsBatchAsync(projectCodes, cancellationToken);
        var cardsTask = LookupBusinessPartnersBatchAsync(cardCodes, cancellationToken);

        await Task.WhenAll(itemsTask, projectsTask, cardsTask);

        return new MasterLookupResponse
        {
            Items = await itemsTask,
            Projects = await projectsTask,
            BusinessPartners = await cardsTask,
        };
    }

    public async Task<List<WithholdingTaxDataCollectionResponse>> GetWithholdingTaxByCodesAsync(
        IReadOnlyList<string> wtCodes,
        CancellationToken cancellationToken = default)
    {
        if (wtCodes.Count == 0)
            return [];

        await sapLogin.SapLoginAsync(cancellationToken);
        var filter = string.Join(" or ", wtCodes.Select(code =>
            $"WTCode eq '{SapPaginationBuilder.EscapeODataString(code)}'"));
        var queries = new SapQueries
        {
            Filter = filter,
            Select = "WTCode,WTName,Rate",
            Top = Math.Min(wtCodes.Count, 50).ToString(),
        };
        var response = await http.GetAsync<GetAllWithholdingTaxDataCollectionResponse>(
            Constants.SapApiUrls.WithholdingTaxCodesCollection + queries.GetQueryValue(),
            checkCache: true,
            cancellationToken: cancellationToken);
        return response?.Value ?? [];
    }

    private async Task<Dictionary<string, string>> LookupItemsBatchAsync(
        IReadOnlyList<string> codes,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (codes.Count == 0)
            return result;

        foreach (var chunk in codes.Chunk(LookupBatchSize))
        {
            var filter = string.Join(" or ", chunk.Select(code => $"ItemCode eq '{SapPaginationBuilder.EscapeODataString(code)}'"));
            var queries = new SapQueries
            {
                Filter = filter,
                Select = "ItemCode,ItemName",
                Top = chunk.Length.ToString(),
            };
            var response = await http.GetAsync<SapItemsResponse>(
                Constants.SapApiUrls.ItemsCollection + queries.GetQueryValue(),
                checkCache: true,
                cancellationToken: cancellationToken);
            foreach (var item in response?.Value ?? [])
            {
                if (!string.IsNullOrWhiteSpace(item.ItemCode))
                    result[item.ItemCode] = item.ItemName ?? string.Empty;
            }
        }

        return result;
    }

    private async Task<Dictionary<string, string>> LookupProjectsBatchAsync(
        IReadOnlyList<string> codes,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (codes.Count == 0)
            return result;

        foreach (var chunk in codes.Chunk(LookupBatchSize))
        {
            var filter = string.Join(" or ", chunk.Select(code => $"Code eq '{SapPaginationBuilder.EscapeODataString(code)}'"));
            var queries = new SapQueries
            {
                Filter = filter,
                Select = "Code,Name",
                Top = chunk.Length.ToString(),
            };
            var response = await http.GetAsync<SapGetAllProjectDetailsResponse>(
                Constants.SapApiUrls.ProjectsCollection + queries.GetQueryValue(),
                checkCache: true,
                cancellationToken: cancellationToken);
            foreach (var project in response?.Value ?? [])
            {
                if (!string.IsNullOrWhiteSpace(project.ProjectCode))
                    result[project.ProjectCode] = project.ProjectName ?? string.Empty;
            }
        }

        return result;
    }

    private async Task<Dictionary<string, string>> LookupBusinessPartnersBatchAsync(
        IReadOnlyList<string> codes,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (codes.Count == 0)
            return result;

        foreach (var chunk in codes.Chunk(LookupBatchSize))
        {
            var filter = string.Join(" or ", chunk.Select(code => $"CardCode eq '{SapPaginationBuilder.EscapeODataString(code)}'"));
            var queries = new SapQueries
            {
                Filter = filter,
                Select = "CardCode,CardName",
                Top = chunk.Length.ToString(),
            };
            var response = await http.GetAsync<SapBusinessPartnerResponse>(
                Constants.SapApiUrls.BusinessPartnersCollection + queries.GetQueryValue(),
                checkCache: true,
                cancellationToken: cancellationToken);
            foreach (var partner in response?.Value ?? [])
            {
                if (!string.IsNullOrWhiteSpace(partner.CardCode))
                    result[partner.CardCode] = partner.CardName ?? string.Empty;
            }
        }

        return result;
    }

    private async Task<PaginationResponse<List<TItem>>> SearchAsync<TResponse, TItem>(
        string collectionUrl,
        SapPaginationOptions profile,
        PaginationRequest request,
        Func<TResponse?, List<TItem>?> getItems,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        await sapLogin.SapLoginAsync(cancellationToken);
        var normalized = PaginationRequest.Normalize(request);
        var queries = SapPaginationBuilder.ToSapQueries(normalized, profile);
        var response = await http.GetAsync<TResponse>(
            collectionUrl + queries.GetQueryValue(),
            checkCache: true,
            cancellationToken: cancellationToken);
        var items = getItems(response) ?? [];
        var totalCount = response is SapBaseResponse sapResponse
            ? SapPaginationBuilder.ResolveTotalCount(sapResponse, items, normalized)
            : SapPaginationBuilder.ResolveTotalCountFromItems(items, normalized);
        return PaginationResponseFactory.Create(normalized, items, totalCount);
    }
}
