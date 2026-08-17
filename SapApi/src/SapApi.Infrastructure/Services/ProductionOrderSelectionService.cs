using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services;

public class ProductionOrderSelectionService(
    SapProductionOrdersService productionOrdersService,
    SapMasterDataService masterDataService)
{
    /// <summary>
    /// Builds the Issue / Receipt from Production selection payload from the local mirror, which
    /// already carries the resolved project name and SAP's numeric line UoMCode. The lines are used
    /// verbatim — swapping in an inventory UoM name such as "KG" makes SAP reject the later write.
    /// </summary>
    public async Task<SapInventoryGenExitRequestOrderLines?> BuildSelectionAsync(
        string absoluteEntry,
        CancellationToken cancellationToken = default)
    {
        var order = await productionOrdersService.GetProductionOrders(
            absoluteEntry,
            cancellationToken: cancellationToken);
        if (order is null) return null;

        return new SapInventoryGenExitRequestOrderLines
        {
            ProductionOrder = order,
            ProductionOrderLinesEntryNumber = [],
        };
    }

    public async Task<ProductionOrderAddLineResult> AddManualLineAsync(
        string absoluteEntry,
        SapProductionOrderLines line,
        CancellationToken cancellationToken = default)
    {
        var selection = await BuildSelectionAsync(absoluteEntry, cancellationToken)
            ?? throw new KeyNotFoundException("Production order not found.");

        var order = selection.ProductionOrder!;
        if (line.IssuedQuantity > line.PlannedQuantity)
            throw new InvalidOperationException("Issue quantity cannot exceed planned quantity.");

        order.ProductionOrderLines ??= [];

        var maxLine = order.ProductionOrderLines
            .Select(x => x.LineNumber ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        line.LineNumber = maxLine + 1;
        line.VisualOrder = order.ProductionOrderLines.Count;
        line.ProductionOrderIssueType = "im_Manual";
        line.Project = order.Project;
        line.BaseQuantity = order.PlannedQuantity > 0
            ? line.PlannedQuantity / order.PlannedQuantity
            : 0;

        var item = await masterDataService.GetItemByCodeAsync(line.ItemNo ?? string.Empty, cancellationToken: cancellationToken);
        line.ItemName = item?.ItemName;
        // Omit UoM fields on manual lines — SAP defaults from item master. Never send InventoryUOM names as UoMCode.
        line.UoMCode = null;
        line.UoMEntry = null;

        var warehouse = await masterDataService.GetWarehouseByCodeAsync(line.Warehouse, cancellationToken: cancellationToken);
        line.LocationCode = warehouse?.Location ?? 0;

        var absoluteEntryInt = order.AbsoluteEntry
            ?? throw new InvalidOperationException("Production order absolute entry is missing.");

        var response = await productionOrdersService.PatchProductionOrderLineAsync(
            absoluteEntryInt,
            line,
            cancellationToken);
        if (response?.Error is not null && !string.IsNullOrEmpty(response.Error.Message?.Value))
            throw new InvalidOperationException($"{response.Error.Code}: {response.Error.Message.Value}");

        var updatedOrder = response ?? order;
        var addedLine = updatedOrder.ProductionOrderLines?
            .FirstOrDefault(l => l.LineNumber == line.LineNumber) ?? line;

        return new ProductionOrderAddLineResult
        {
            AddedLine = addedLine,
            ProductionOrder = updatedOrder,
        };
    }
}
