using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Identity;
using SapApi.Infrastructure.Jobs;
using SapApi.Infrastructure.Services.Items;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Configuration;
using SapApi.Shared.Models;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/masters")]
[Authorize]
public class MastersController(
    SapMasterDataService masterDataService,
    ItemLocalStore itemLocalStore,
    ICurrentCompanyDbAccessor companyDbAccessor,
    IHttpContextAccessor httpContextAccessor,
    IOptions<HangfireOptions> hangfireOptions,
    IServiceProvider services) : ControllerBase
{
    /// <summary>
    /// Item search — reads the local Postgres mirror (kept in sync by <see cref="ItemSyncJob"/>)
    /// instead of hitting SAP live on every keystroke. Group-name filters (e.g. "Consumable") are
    /// still resolved against SAP's small, stable ItemGroups list first.
    /// </summary>
    [HttpPost("items/list")]
    public async Task<IActionResult> ListItems([FromBody] PaginationRequest? request, CancellationToken cancellationToken)
    {
        var normalized = PaginationRequest.Normalize(request);
        var (groupCodes, remaining) = await masterDataService.ResolveItemGroupCodesAsync(normalized, cancellationToken);
        return Ok(await itemLocalStore.ListFromDbAsync(remaining, groupCodes, cancellationToken));
    }

    [HttpGet("items/sync-status")]
    public async Task<IActionResult> ItemSyncStatus(CancellationToken cancellationToken)
    {
        var status = await itemLocalStore.GetSyncStateAsync(cancellationToken);
        return Ok(ApiResponse<object?>.Ok(status));
    }

    /// <summary>
    /// Enqueues a Hangfire job that fully syncs the SAP item catalog for the current company.
    /// Returns the existing job if one is already Running.
    /// </summary>
    [HttpPost("items/sync/jobs/full")]
    public async Task<IActionResult> EnqueueItemSyncJob(CancellationToken cancellationToken)
    {
        var hangfire = hangfireOptions.Value;
        var backgroundJobs = services.GetService<IBackgroundJobClient>();
        if (!hangfire.Enabled || backgroundJobs is null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail(
                    BaseErrorCodes.ValidationFailed,
                    "Item background sync is unavailable (Hangfire is disabled)."));
        }

        var existing = await itemLocalStore.GetSyncStateAsync(cancellationToken);
        if (existing is not null
            && string.Equals(existing.Status, ItemSyncState.StatusRunning, StringComparison.OrdinalIgnoreCase))
        {
            return Ok(ApiResponse<object>.Ok(new
            {
                jobId = existing.HangfireJobId,
                status = existing.Status,
                message = existing.Message,
                alreadyRunning = true,
            }));
        }

        var started = await itemLocalStore.TryBeginFullSyncJobAsync(hangfireJobId: null, cancellationToken);
        if (!started)
        {
            var raced = await itemLocalStore.GetSyncStateAsync(cancellationToken);
            return Ok(ApiResponse<object>.Ok(new
            {
                jobId = raced?.HangfireJobId,
                status = raced?.Status ?? ItemSyncState.StatusRunning,
                message = raced?.Message ?? "Full sync already running.",
                alreadyRunning = true,
            }));
        }

        var companyDb = companyDbAccessor.GetCompanyDbName();
        var requestingUserId = httpContextAccessor.GetUserIdAsync() ?? hangfire.ServiceUserId;
        string jobId;
        try
        {
            jobId = backgroundJobs.Enqueue<ItemSyncJob>(
                job => job.ExecuteAsync(companyDb, requestingUserId, null!, CancellationToken.None));
        }
        catch (Exception ex)
        {
            await itemLocalStore.MarkFullSyncFailedAsync($"Failed to enqueue full sync job: {ex.Message}", cancellationToken);
            throw;
        }

        await itemLocalStore.SetFullSyncJobIdAsync(jobId, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new
        {
            jobId,
            status = ItemSyncState.StatusRunning,
            message = "Full sync job queued.",
            alreadyRunning = false,
        }));
    }

    /// <summary>
    /// Synchronous, resumable full sync batch — browser-driven fallback when Hangfire is disabled.
    /// Pass afterItemCode=lastItemCode from the previous response while hasMore=true.
    /// </summary>
    [HttpPost("items/sync/full")]
    public async Task<IActionResult> SyncItemsFull([FromQuery] string? afterItemCode, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await itemLocalStore.SyncAllFromSapAsync(afterItemCode, cancellationToken)));

    /// <summary>Refresh a single item from SAP into the local table.</summary>
    [HttpPost("items/{itemCode}/sync")]
    public async Task<IActionResult> SyncOneItem(string itemCode, CancellationToken cancellationToken) =>
        Ok(ApiResponse<object>.Ok(await itemLocalStore.SyncOneFromSapAsync(itemCode, cancellationToken)));

    [HttpGet("items/{itemCode}")]
    public async Task<IActionResult> GetItem(string itemCode, [FromQuery] string? fields, CancellationToken cancellationToken)
    {
        var item = await masterDataService.GetItemByCodeAsync(itemCode, ParseFields(fields), cancellationToken);
        return item is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Item not found"))
            : Ok(ApiResponse<object>.Ok(item));
    }

    /// <summary>
    /// Purchase UoM options for a line of this item: the item's UoM group when it has one, otherwise
    /// the UoM master (SAP's "Manual" group defines no per-item units).
    /// </summary>
    [HttpGet("items/{itemCode}/purchase-uoms")]
    public async Task<IActionResult> GetItemPurchaseUoms(
        string itemCode,
        [FromQuery] string? search,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<List<PurchaseUomOptionResponse>>.Ok(
            await masterDataService.GetPurchaseUomOptionsAsync(itemCode, search, cancellationToken)));

    /// <summary>
    /// The full UoM master — for pickers with no item to derive units from, such as a purchase
    /// order's service lines.
    /// </summary>
    [HttpGet("uoms")]
    public async Task<IActionResult> GetUnitOfMeasurements(
        [FromQuery] string? search,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse<List<PurchaseUomOptionResponse>>.Ok(
            await masterDataService.GetAllUnitOfMeasurementsAsync(search, cancellationToken)));

    [HttpPost("warehouses/list")]
    public async Task<IActionResult> ListWarehouses([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchWarehousesAsync(PaginationRequest.Normalize(request), cancellationToken));

    /// <summary>Full warehouse address (Street/Block/City/State/Zip) — used to auto-fill Dispatch
    /// Address on PO/PR forms when the branch's Dispatch Location resolves to a Factory/Office
    /// warehouse (see Sheet3 table: those locations ship from the warehouse's own address).</summary>
    [HttpGet("warehouses/{warehouseCode}/address")]
    public async Task<IActionResult> GetWarehouseAddress(string warehouseCode, CancellationToken cancellationToken)
    {
        var warehouse = await masterDataService.GetWarehouseByCodeAsync(warehouseCode, cancellationToken: cancellationToken);
        if (warehouse is null)
            return NotFound(ApiResponse<object>.Fail("SYS-02", "Warehouse not found"));

        var parts = new[] { warehouse.StreetNo, warehouse.Street, warehouse.BuildingFloorRoom, warehouse.Block, warehouse.City }
            .Select(p => (p ?? string.Empty).Trim())
            .Where(p => p.Length > 0);

        return Ok(ApiResponse<object>.Ok(new
        {
            warehouseCode = warehouse.WarehouseCode,
            warehouseName = warehouse.WarehouseName,
            formattedAddress = string.Join(", ", parts),
            city = warehouse.City,
            state = warehouse.State,
            zipCode = warehouse.ZipCode,
        }));
    }

    [HttpPost("tax-codes/list")]
    public async Task<IActionResult> ListTaxCodes([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchTaxCodesAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpPost("gl-accounts/list")]
    public async Task<IActionResult> ListGlAccounts([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchChartOfAccountsAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpPost("projects/list")]
    public async Task<IActionResult> ListProjects([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchProjectsAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpPost("hsn-codes/list")]
    public async Task<IActionResult> ListHsnCodes([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchHsnCodesAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpPost("sac-codes/list")]
    public async Task<IActionResult> ListSacCodes([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchSacCodesAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpPost("sales-persons/list")]
    public async Task<IActionResult> ListSalesPersons([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchSalesPersonsAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpGet("sales-persons/{salesEmployeeCode:int}")]
    public async Task<IActionResult> GetSalesPerson(int salesEmployeeCode, CancellationToken cancellationToken)
    {
        var person = await masterDataService.GetSalesPersonByCodeAsync(salesEmployeeCode, cancellationToken);
        return person is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Sales person not found"))
            : Ok(ApiResponse<object>.Ok(person));
    }

    [HttpPost("employees/list")]
    public async Task<IActionResult> ListEmployees([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchEmployeesAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpGet("employees/{employeeId:int}")]
    public async Task<IActionResult> GetEmployee(int employeeId, CancellationToken cancellationToken)
    {
        var employee = await masterDataService.GetEmployeeByIdAsync(employeeId, cancellationToken);
        return employee is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Employee not found"))
            : Ok(ApiResponse<object>.Ok(employee));
    }

    [HttpGet("projects/{projectCode}")]
    public async Task<IActionResult> GetProject(string projectCode, [FromQuery] string? fields, CancellationToken cancellationToken)
    {
        var project = await masterDataService.GetProjectByCodeAsync(projectCode, ParseFields(fields), cancellationToken);
        return project is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Project not found"))
            : Ok(ApiResponse<object>.Ok(project));
    }

    [HttpPost("lookup")]
    public async Task<IActionResult> Lookup([FromBody] MasterLookupRequest? request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<MasterLookupResponse>.Ok(await masterDataService.LookupMasterDataAsync(request ?? new MasterLookupRequest(), cancellationToken)));

    [HttpGet("payment-term-types")]
    public async Task<IActionResult> GetPaymentTermTypes(CancellationToken cancellationToken) =>
        Ok(ApiResponse<List<PaymentTermTypeOption>>.Ok(await masterDataService.GetPaymentTermTypesAsync(cancellationToken)));

    [HttpGet("purchase-order-logistics-options")]
    public async Task<IActionResult> GetPurchaseOrderLogisticsOptions(CancellationToken cancellationToken) =>
        Ok(ApiResponse<PurchaseOrderLogisticsOptions>.Ok(
            await masterDataService.GetPurchaseOrderLogisticsOptionsAsync(cancellationToken)));

    [HttpPost("business-places/list")]
    public async Task<IActionResult> ListBusinessPlaces([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchBusinessPlacesAsync(PaginationRequest.Normalize(request), cancellationToken));

    [HttpPost("sales-orders/list")]
    public async Task<IActionResult> ListSalesOrders(
        [FromBody] PaginationRequest? request,
        [FromQuery] string? customerId,
        CancellationToken cancellationToken) =>
        Ok(await masterDataService.SearchSalesOrdersAsync(PaginationRequest.Normalize(request), customerId, cancellationToken));

    [HttpGet("sales-orders/{docEntry:int}")]
    public async Task<IActionResult> GetSalesOrder(int docEntry, CancellationToken cancellationToken)
    {
        var order = await masterDataService.GetSalesOrderByDocEntryAsync(docEntry, cancellationToken);
        return order is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Sales order not found"))
            : Ok(ApiResponse<object>.Ok(order));
    }

    /// <summary>Comma-separated list of field names the caller actually needs, e.g. "ItemCode,ItemName".</summary>
    private static List<string>? ParseFields(string? fields) =>
        string.IsNullOrWhiteSpace(fields)
            ? null
            : fields.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
}
