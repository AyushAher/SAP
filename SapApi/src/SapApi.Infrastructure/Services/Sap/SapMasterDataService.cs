using System.Globalization;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Sap;
using SapApi.Shared;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Account;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Infrastructure.Services.Sap;

public class SapMasterDataService(
    IHttpRequestHandler http,
    ISapLoginService sapLogin,
    ISapMasterDataCache cache,
    ICurrentCompanyDbAccessor companyDbAccessor)
{
    private const int LookupBatchSize = 20;

    /// <summary>
    /// Master data (vendors, items, warehouses, projects, tax codes, business places) changes rarely
    /// enough that a 1-hour cache is safe. Transactional documents (sales orders, PO/payment lookups)
    /// must never be cached and always go straight to SAP — see the `cacheable: false` call sites below.
    /// </summary>
    private static readonly TimeSpan MasterDataCacheTtl = TimeSpan.FromHours(1);

    /// <summary>UDF ValidValues metadata changes rarely; short TTL so ensure-updates are visible soon.</summary>
    private static readonly TimeSpan PaymentTermTypeCacheTtl = TimeSpan.FromMinutes(20);

    private const int PaymentTermTypeUdfMinSize = 20;
    private static readonly string[] PaymentTermTypeUdfNames =
        Enumerable.Range(1, 10).Select(i => $"T{i}").ToArray();

    private static readonly string[] ItemLookupKeyFields = ["ItemCode"];
    private static readonly string[] BusinessPartnerLookupKeyFields = ["CardCode"];
    private static readonly string[] WarehouseLookupKeyFields = ["WarehouseCode"];
    private static readonly string[] ProjectLookupKeyFields = ["Code"];
    private static readonly string[] BusinessPlaceLookupKeyFields = ["BPLID"];

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

    public Task<PaginationResponse<List<SapChartOfAccountResponse>>> SearchChartOfAccountsAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default) =>
        SearchAsync<GetAllSapChartOfAccountsResponse, SapChartOfAccountResponse>(
            Constants.SapApiUrls.ChartOfAccountsCollection,
            SapPaginationProfiles.ChartOfAccounts,
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

    public async Task<PaginationResponse<List<IndiaHsnCodeResponse>>> SearchHsnCodesAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        await sapLogin.SapLoginAsync(cancellationToken);
        var normalized = PaginationRequest.Normalize(request);
        var all = await GetCachedIndiaHsnListAsync(cancellationToken);
        return FilterAndPage(
            all,
            normalized,
            item => string.Join(' ', item.AbsEntry, item.ChapterID, item.Chapter, item.Heading, item.SubHeading, item.Description, item.Dscription));
    }

    public async Task<PaginationResponse<List<IndiaSacCodeResponse>>> SearchSacCodesAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default)
    {
        await sapLogin.SapLoginAsync(cancellationToken);
        var normalized = PaginationRequest.Normalize(request);
        var all = await GetCachedIndiaSacListAsync(cancellationToken);
        return FilterAndPage(
            all,
            normalized,
            item => string.Join(' ', item.AbsEntry, item.ServiceCode, item.Description));
    }

    private async Task<List<IndiaHsnCodeResponse>> GetCachedIndiaHsnListAsync(CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            $"masterdata:{companyDbAccessor.GetCompanyDbName()}:IndiaHsnService_GetList",
            async () =>
            {
                try
                {
                    var envelope = await http.PostAsync<object, IndiaHsnListEnvelope>(
                        Constants.SapApiUrls.IndiaHsnServiceGetList,
                        new { },
                        cancellationToken);
                    return envelope?.Value ?? envelope?.IndiaHsn ?? [];
                }
                catch (Exception)
                {
                    // Company may not expose India HSN service — UI still allows manual AbsEntry.
                    return [];
                }
            },
            MasterDataCacheTtl,
            cancellationToken) ?? [];

    private async Task<List<IndiaSacCodeResponse>> GetCachedIndiaSacListAsync(CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            $"masterdata:{companyDbAccessor.GetCompanyDbName()}:IndiaSacService_GetList",
            async () =>
            {
                try
                {
                    var envelope = await http.PostAsync<object, IndiaSacListEnvelope>(
                        Constants.SapApiUrls.IndiaSacServiceGetList,
                        new { },
                        cancellationToken);
                    return envelope?.Value ?? envelope?.IndiaSac ?? [];
                }
                catch (Exception)
                {
                    return [];
                }
            },
            MasterDataCacheTtl,
            cancellationToken) ?? [];

    private static PaginationResponse<List<TItem>> FilterAndPage<TItem>(
        IReadOnlyList<TItem> all,
        PaginationRequest request,
        Func<TItem, string> searchText)
    {
        IEnumerable<TItem> query = all;
        var search = request.Filters
            .FirstOrDefault(f => f.Field.Equals("__search", StringComparison.OrdinalIgnoreCase))
            ?.Value?.ToString()
            ?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(item =>
                searchText(item).Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query.ToList();
        var page = Math.Max(1, request.PageNumber);
        var size = Math.Clamp(request.PageSize ?? 20, 1, 200);
        var items = filtered.Skip((page - 1) * size).Take(size).ToList();
        return PaginationResponseFactory.Create(request, items, filtered.Count);
    }

    public Task<PaginationResponse<List<SapBranchesResponse>>> SearchBusinessPlacesAsync(PaginationRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync<SapGetAllBranchesResponse, SapBranchesResponse>(
            Constants.SapApiUrls.BusinessPlacesCollection,
            SapPaginationProfiles.BusinessPlaces,
            request,
            r => r?.Value,
            cancellationToken);

    public async Task<List<BranchOptionResponse>> ListBranchOptionsAsync(CancellationToken cancellationToken = default)
    {
        await sapLogin.SapLoginAsync(cancellationToken);
        var queries = new SapQueries
        {
            Select = "BPLID,BPLName",
            Top = "500",
        };
        var response = await GetCachedAsync<SapGetAllBranchesResponse>(
            Constants.SapApiUrls.BusinessPlacesCollection + queries.GetQueryValue(),
            cancellationToken);

        return response?.Value?
            .Select(branch => new BranchOptionResponse
            {
                Id = branch.BplId,
                Name = string.IsNullOrWhiteSpace(branch.BplName) ? $"Branch {branch.BplId}" : branch.BplName,
            })
            .OrderBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    public Task<PaginationResponse<List<SapBusinessPartner>>> SearchVendorsAsync(PaginationRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync<SapBusinessPartnerResponse, SapBusinessPartner>(
            Constants.SapApiUrls.BusinessPartnersCollection,
            SapPaginationProfiles.Vendors,
            request,
            r => r?.Value,
            cancellationToken);

    public Task<PaginationResponse<List<SapSalesPersonResponse>>> SearchSalesPersonsAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default) =>
        SearchAsync<GetAllSapSalesPersonsResponse, SapSalesPersonResponse>(
            Constants.SapApiUrls.SalesPersonsCollection,
            SapPaginationProfiles.SalesPersons,
            request,
            r => r?.Value,
            cancellationToken);

    public async Task<SapSalesPersonResponse?> GetSalesPersonByCodeAsync(
        int salesEmployeeCode,
        CancellationToken cancellationToken = default)
    {
        await sapLogin.SapLoginAsync(cancellationToken);
        try
        {
            return await GetCachedAsync<SapSalesPersonResponse>(
                $"{Constants.SapApiUrls.SalesPersonsCollection}({salesEmployeeCode})",
                cancellationToken);
        }
        catch
        {
            // Fall back to filtered list when key addressing is unavailable.
            var page = await SearchSalesPersonsAsync(new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 5,
                Filters = [new FilterModel { Field = "__search", Operator = "contains", Value = salesEmployeeCode.ToString(CultureInfo.InvariantCulture) }],
            }, cancellationToken);
            return page.Data?.FirstOrDefault(sp => sp.SalesEmployeeCode == salesEmployeeCode);
        }
    }

    public Task<PaginationResponse<List<SapEmployeeInfoResponse>>> SearchEmployeesAsync(
        PaginationRequest request,
        CancellationToken cancellationToken = default) =>
        SearchAsync<GetAllSapEmployeesInfoResponse, SapEmployeeInfoResponse>(
            Constants.SapApiUrls.EmployeesInfoCollection,
            SapPaginationProfiles.EmployeesInfo,
            request,
            r => r?.Value,
            cancellationToken);

    public async Task<SapEmployeeInfoResponse?> GetEmployeeByIdAsync(
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        await sapLogin.SapLoginAsync(cancellationToken);
        try
        {
            return await GetCachedAsync<SapEmployeeInfoResponse>(
                $"{Constants.SapApiUrls.EmployeesInfoCollection}({employeeId})",
                cancellationToken);
        }
        catch
        {
            var page = await SearchEmployeesAsync(new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 5,
                Filters = [new FilterModel { Field = "__search", Operator = "contains", Value = employeeId.ToString(CultureInfo.InvariantCulture) }],
            }, cancellationToken);
            return page.Data?.FirstOrDefault(emp => emp.EmployeeID == employeeId);
        }
    }

    /// <summary>
    /// Returns OPOR payment-term type options from UserFieldsMD T1 ValidValues, merged with app extras.
    /// Best-effort ensure of extras on T1–T10 runs once per cache miss (append-only).
    /// </summary>
    public async Task<List<PaymentTermTypeOption>> GetPaymentTermTypesAsync(CancellationToken cancellationToken = default)
    {
        await sapLogin.SapLoginAsync(cancellationToken);
        var companyDb = companyDbAccessor.GetCompanyDbName();
        var cacheKey = $"masterdata:{companyDb}:payment-term-types";

        var cached = await cache.GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                try
                {
                    await EnsurePaymentTermTypeValidValuesAsync(cancellationToken);
                }
                catch
                {
                    // Still serve SAP list + extras even if ensure fails.
                }

                var sapOptions = await FetchPaymentTermTypesFromSapAsync(cancellationToken);
                return PaymentTermTypeOptions.MergeWithExtras(sapOptions);
            },
            PaymentTermTypeCacheTtl,
            cancellationToken);

        return cached ?? PaymentTermTypeOptions.FallbackWithExtras();
    }

    /// <summary>
    /// Appends missing GstProforma/TaxInvoice ValidValues on OPOR T1–T10 (when those UDFs exist).
    /// Increases Size when needed so GstProforma (11 chars) fits. Never removes existing values.
    /// </summary>
    public async Task EnsurePaymentTermTypeValidValuesAsync(CancellationToken cancellationToken = default)
    {
        await sapLogin.SapLoginAsync(cancellationToken);

        foreach (var name in PaymentTermTypeUdfNames)
        {
            var field = await GetOporTypeUserFieldAsync(name, cancellationToken);
            if (field?.FieldID is null)
                continue;

            var fieldId = field.FieldID.Value;
            var size = field.Size ?? 0;
            if (size < PaymentTermTypeUdfMinSize)
            {
                await http.PatchAsync<object, object>(
                    Constants.SapApiUrls.UserFieldsMd("OPOR", fieldId),
                    new { Size = PaymentTermTypeUdfMinSize, EditSize = PaymentTermTypeUdfMinSize },
                    cancellationToken);
            }

            var existing = new HashSet<string>(
                (field.ValidValuesMD ?? [])
                    .Select(v => v.Value?.Trim() ?? string.Empty)
                    .Where(v => v.Length > 0),
                StringComparer.OrdinalIgnoreCase);

            var missing = PaymentTermTypeOptions.AppExtras
                .Where(extra => !existing.Contains(extra.Value))
                .Select(extra => new SapUserFieldsMdValidValue
                {
                    Value = extra.Value,
                    Description = extra.Description,
                })
                .ToList();

            if (missing.Count == 0)
                continue;

            // PATCH with only new ValidValues appends; sending existing values causes Duplicate value.
            await http.PatchAsync<object, object>(
                Constants.SapApiUrls.UserFieldsMd("OPOR", fieldId),
                new { ValidValuesMD = missing },
                cancellationToken);
        }
    }

    private async Task<List<PaymentTermTypeOption>> FetchPaymentTermTypesFromSapAsync(CancellationToken cancellationToken)
    {
        var field = await GetOporTypeUserFieldAsync("T1", cancellationToken);
        if (field?.ValidValuesMD is null || field.ValidValuesMD.Count == 0)
            return PaymentTermTypeOptions.SapDefaults.ToList();

        return field.ValidValuesMD
            .Where(v => !string.IsNullOrWhiteSpace(v.Value))
            .Select(v => new PaymentTermTypeOption
            {
                Value = v.Value!.Trim(),
                Description = string.IsNullOrWhiteSpace(v.Description) ? v.Value.Trim() : v.Description.Trim(),
            })
            .ToList();
    }

    private async Task<SapUserFieldsMdResponse?> GetOporTypeUserFieldAsync(string name, CancellationToken cancellationToken)
    {
        var filter = $"TableName eq 'OPOR' and Name eq '{SapPaginationBuilder.EscapeODataString(name)}'";
        var url = $"{Constants.SapApiUrls.UserFieldsMdCollection}?$filter={Uri.EscapeDataString(filter)}";
        var response = await http.GetAsync<GetAllSapUserFieldsMdResponse>(url, cancellationToken: cancellationToken);
        return response?.Value?.FirstOrDefault();
    }

    public Task<PaginationResponse<List<SapBusinessPartner>>> SearchCustomersAsync(PaginationRequest request, CancellationToken cancellationToken = default) =>
        SearchAsync<SapBusinessPartnerResponse, SapBusinessPartner>(
            Constants.SapApiUrls.BusinessPartnersCollection,
            SapPaginationProfiles.Customers,
            request,
            r => r?.Value,
            cancellationToken);

    /// <summary>Sales orders are transactional documents, not master data — never cached.</summary>
    public Task<PaginationResponse<List<SapSalesOrderResponse>>> SearchSalesOrdersAsync(
        PaginationRequest request,
        string? customerId = null,
        CancellationToken cancellationToken = default) =>
        SearchAsync<GetAllSapSalesOrderResponse, SapSalesOrderResponse>(
            Constants.SapApiUrls.OrdersCollection,
            SapPaginationProfiles.SalesOrders(customerId),
            request,
            r => r?.Value,
            cancellationToken,
            cacheable: false);

    public async Task<SapBusinessPartner?> GetBusinessPartnerByCardCodeAsync(
        string cardCode,
        IReadOnlyList<string>? fields = null,
        CancellationToken cancellationToken = default)
    {
        await sapLogin.SapLoginAsync(cancellationToken);
        var safeCode = SapPaginationBuilder.EscapeODataString(cardCode);
        var queries = new SapQueries
        {
            Filter = $"CardCode eq '{safeCode}'",
            Select = SapPaginationBuilder.ResolveSelect(
                "CardCode,CardName,CardType,BPWithholdingTaxCollection", BusinessPartnerLookupKeyFields, fields),
            Top = "1",
        };
        var response = await GetCachedAsync<SapBusinessPartnerResponse>(
            Constants.SapApiUrls.BusinessPartnersCollection + queries.GetQueryValue(),
            cancellationToken);
        return response?.Value?.FirstOrDefault();
    }

    /// <summary>
    /// Addresses + contact employees for PO Logistics (Dispatch Address / Contact Person).
    /// </summary>
    public async Task<BusinessPartnerLogisticsDetails?> GetBusinessPartnerLogisticsAsync(
        string cardCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cardCode))
            return null;

        await sapLogin.SapLoginAsync(cancellationToken);
        var safeCode = SapPaginationBuilder.EscapeODataString(cardCode.Trim());
        // Key lookup returns BPAddresses + ContactEmployees without $expand on this SL version.
        var url = $"{Constants.SapApiUrls.BusinessPartnersCollection}('{safeCode}')";
        var bp = await http.GetAsync<SapBusinessPartner>(url, cancellationToken: cancellationToken);
        if (bp?.CardCode is null)
            return null;

        var addresses = (bp.BPAddresses ?? [])
            .Select(a =>
            {
                var name = (a.AddressName ?? string.Empty).Trim();
                var formatted = FormatBpAddress(a);
                if (string.IsNullOrWhiteSpace(formatted) && string.IsNullOrWhiteSpace(name))
                    return null;
                return new BusinessPartnerAddressOption
                {
                    AddressName = name,
                    AddressType = (a.AddressType ?? string.Empty).Trim(),
                    FormattedAddress = string.IsNullOrWhiteSpace(formatted) ? name : formatted,
                };
            })
            .Where(a => a is not null)
            .Cast<BusinessPartnerAddressOption>()
            .ToList();

        var contacts = (bp.ContactEmployees ?? [])
            .Where(c => !string.Equals(c.Active, Constants.SapBoolean.SapFalse, StringComparison.OrdinalIgnoreCase))
            .Select(c =>
            {
                var name = (c.Name ?? $"{c.FirstName} {c.LastName}".Trim()).Trim();
                if (string.IsNullOrWhiteSpace(name))
                    return null;
                return new BusinessPartnerContactOption
                {
                    InternalCode = c.InternalCode,
                    Name = name,
                    Position = c.Position,
                    Phone = string.IsNullOrWhiteSpace(c.MobilePhone) ? c.Phone1 : c.MobilePhone,
                };
            })
            .Where(c => c is not null)
            .Cast<BusinessPartnerContactOption>()
            .ToList();

        return new BusinessPartnerLogisticsDetails
        {
            CardCode = bp.CardCode,
            CardName = bp.CardName,
            DefaultShipTo = bp.ShipToDefault,
            DefaultContactPerson = bp.ContactPerson,
            Addresses = addresses,
            Contacts = contacts,
        };
    }

    private static string FormatBpAddress(SapBusinessPartnerAddress address)
    {
        var parts = new[]
        {
            address.AddressName2,
            address.AddressName3,
            address.Street,
            address.StreetNo,
            address.BuildingFloorRoom,
            address.Block,
            address.City,
            address.State,
            address.ZipCode,
            address.Country,
        };

        var formatted = string.Join(", ",
            parts
                .Select(p => (p ?? string.Empty).Trim())
                .Where(p => p.Length > 0));

        // ADOC U_DispachAdd Size = 120
        return formatted.Length <= 120 ? formatted : formatted[..120];
    }

    public async Task<ItemsResponse?> GetItemByCodeAsync(
        string itemCode,
        IReadOnlyList<string>? fields = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemCode))
            return null;

        await sapLogin.SapLoginAsync(cancellationToken);
        var safeCode = SapPaginationBuilder.EscapeODataString(itemCode);
        var queries = new SapQueries
        {
            Filter = $"ItemCode eq '{safeCode}'",
            Select = SapPaginationBuilder.ResolveSelect(
                "ItemCode,ItemName,ItemsGroupCode,InventoryItem,InventoryUOM,InventoryWeight,PurchaseUnit,PurchaseItemsPerUnit,PurchaseVATGroup,ChapterID,DefaultWarehouse",
                ItemLookupKeyFields,
                fields ?? ["ItemCode", "ItemName", "InventoryUOM", "PurchaseUnit", "PurchaseItemsPerUnit", "InventoryWeight", "PurchaseVATGroup", "ChapterID", "DefaultWarehouse"]),
            Top = "1",
        };
        var response = await GetCachedAsync<SapItemsResponse>(
            Constants.SapApiUrls.ItemsCollection + queries.GetQueryValue(),
            cancellationToken);
        return response?.Value?.FirstOrDefault();
    }

    public async Task<WarehouseResponse?> GetWarehouseByCodeAsync(
        string? warehouseCode,
        IReadOnlyList<string>? fields = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(warehouseCode))
            return null;

        await sapLogin.SapLoginAsync(cancellationToken);
        var safeCode = SapPaginationBuilder.EscapeODataString(warehouseCode);
        var queries = new SapQueries
        {
            Filter = $"WarehouseCode eq '{safeCode}'",
            Select = SapPaginationBuilder.ResolveSelect(
                "WarehouseCode,WarehouseName,State,City,Location", WarehouseLookupKeyFields, fields),
            Top = "1",
        };
        var response = await GetCachedAsync<SapWarehousesResponse>(
            Constants.SapApiUrls.WarehousesCollection + queries.GetQueryValue(),
            cancellationToken);
        return response?.Value?.FirstOrDefault();
    }

    public async Task<SapProjectDetailsResponse?> GetProjectByCodeAsync(
        string? projectCode,
        IReadOnlyList<string>? fields = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectCode))
            return null;

        await sapLogin.SapLoginAsync(cancellationToken);
        var safeCode = SapPaginationBuilder.EscapeODataString(projectCode);
        var queries = new SapQueries
        {
            Filter = $"Code eq '{safeCode}'",
            Select = SapPaginationBuilder.ResolveSelect("Code,Name", ProjectLookupKeyFields, fields),
            Top = "1",
        };
        var response = await GetCachedAsync<SapGetAllProjectDetailsResponse>(
            Constants.SapApiUrls.ProjectsCollection + queries.GetQueryValue(),
            cancellationToken);
        return response?.Value?.FirstOrDefault();
    }

    public async Task<SapBranchesResponse?> GetBusinessPlaceByIdAsync(
        int? bplId,
        IReadOnlyList<string>? fields = null,
        CancellationToken cancellationToken = default)
    {
        if (bplId is null)
            return null;

        await sapLogin.SapLoginAsync(cancellationToken);
        var queries = new SapQueries
        {
            Filter = $"BPLID eq {bplId.Value}",
            Select = SapPaginationBuilder.ResolveSelect("BPLID,BPLName,Address", BusinessPlaceLookupKeyFields, fields),
            Top = "1",
        };
        var response = await GetCachedAsync<SapGetAllBranchesResponse>(
            Constants.SapApiUrls.BusinessPlacesCollection + queries.GetQueryValue(),
            cancellationToken);
        return response?.Value?.FirstOrDefault();
    }

    public async Task<string?> GetProjectNameAsync(string? projectCode, CancellationToken cancellationToken = default)
    {
        var project = await GetProjectByCodeAsync(projectCode, cancellationToken: cancellationToken);
        return project?.ProjectName;
    }

    /// <summary>
    /// Prefetches the first page of every master-data collection using the same field subsets the UI
    /// typeaheads request, plus the full default select used by server-side callers. Replaces (and
    /// extends) the 1-hour Redis TTL so Hangfire can refresh before entries expire.
    /// </summary>
    public async Task WarmCacheAsync(CancellationToken cancellationToken = default)
    {
        // Match sap-ui Requests/masters.ts default field constants so cache keys align with live traffic.
        string[] itemDropdown = ["ItemCode", "ItemName"];
        string[] itemDetail = ["ItemCode", "ItemName", "InventoryUOM", "PurchaseUnit", "PurchaseItemsPerUnit", "InventoryWeight", "PurchaseVATGroup", "ChapterID", "DefaultWarehouse"];
        string[] warehouseDropdown = ["WarehouseCode", "WarehouseName"];
        string[] taxDropdown = ["Code", "Name", "Rate"];
        string[] projectDropdown = ["Code", "Name"];
        string[] partnerDropdown = ["CardCode", "CardName"];

        await SearchItemsAsync(Page(itemDropdown), cancellationToken);
        await SearchItemsAsync(Page(itemDetail), cancellationToken);
        await SearchItemsAsync(Page(null), cancellationToken);

        await SearchWarehousesAsync(Page(warehouseDropdown), cancellationToken);
        await SearchWarehousesAsync(Page(null), cancellationToken);

        await SearchTaxCodesAsync(Page(taxDropdown), cancellationToken);
        await SearchProjectsAsync(Page(projectDropdown), cancellationToken);

        await SearchVendorsAsync(Page(partnerDropdown), cancellationToken);
        await SearchVendorsAsync(Page(null), cancellationToken);
        await SearchCustomersAsync(Page(partnerDropdown), cancellationToken);
        await SearchCustomersAsync(Page(null), cancellationToken);

        await SearchBusinessPlacesAsync(Page(null), cancellationToken);
        await ListBranchOptionsAsync(cancellationToken);
    }

    private static PaginationRequest Page(IReadOnlyList<string>? fields) => new()
    {
        PageNumber = 1,
        PageSize = 20,
        Fields = fields is null ? null : fields.ToList(),
    };

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
        var response = await GetCachedAsync<GetAllWithholdingTaxDataCollectionResponse>(
            Constants.SapApiUrls.WithholdingTaxCodesCollection + queries.GetQueryValue(),
            cancellationToken);
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
            var response = await GetCachedAsync<SapItemsResponse>(
                Constants.SapApiUrls.ItemsCollection + queries.GetQueryValue(),
                cancellationToken);
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
            var response = await GetCachedAsync<SapGetAllProjectDetailsResponse>(
                Constants.SapApiUrls.ProjectsCollection + queries.GetQueryValue(),
                cancellationToken);
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
            var response = await GetCachedAsync<SapBusinessPartnerResponse>(
                Constants.SapApiUrls.BusinessPartnersCollection + queries.GetQueryValue(),
                cancellationToken);
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
        CancellationToken cancellationToken,
        bool cacheable = true)
        where TResponse : class
    {
        await sapLogin.SapLoginAsync(cancellationToken);
        var normalized = PaginationRequest.Normalize(request);
        var queries = SapPaginationBuilder.ToSapQueries(normalized, profile);
        var url = collectionUrl + queries.GetQueryValue();
        var response = cacheable
            ? await GetCachedAsync<TResponse>(url, cancellationToken)
            : await http.GetAsync<TResponse>(url, cancellationToken: cancellationToken);
        var items = getItems(response) ?? [];
        var totalCount = response is SapBaseResponse sapResponse
            ? SapPaginationBuilder.ResolveTotalCount(sapResponse, items, normalized)
            : SapPaginationBuilder.ResolveTotalCountFromItems(items, normalized);
        return PaginationResponseFactory.Create(normalized, items, totalCount);
    }

    /// <summary>
    /// Wraps a SAP GET behind the 1-hour master-data cache. The full request URL (collection + resolved
    /// $select/$filter/$orderby/$skip/$top) is already the exact, unique signature of what's being
    /// asked for, so it doubles as the cache key — different filters, field subsets, or pages simply
    /// produce different keys and are cached independently. Namespaced per company DB so tenants never
    /// share cache entries.
    /// </summary>
    private Task<T?> GetCachedAsync<T>(string url, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            $"masterdata:{companyDbAccessor.GetCompanyDbName()}:{url}",
            () => http.GetAsync<T>(url, cancellationToken: cancellationToken),
            MasterDataCacheTtl,
            cancellationToken);
}
