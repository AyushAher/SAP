using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Infrastructure.Services;

public class ProductionOrderSelectionService(
    SapProductionOrdersService productionOrdersService,
    SapMasterDataService masterDataService)
{
    public async Task<SapInventoryGenExitRequestOrderLines?> BuildSelectionAsync(
        string absoluteEntry,
        CancellationToken cancellationToken = default)
    {
        var order = await productionOrdersService.GetProductionOrders(absoluteEntry);
        if (order is null) return null;

        var linesResponse = await productionOrdersService.GetProductionOrderLines(absoluteEntry);
        order.ProductionOrderLines = linesResponse?.Value ?? [];

        if (string.IsNullOrEmpty(order.ProjectName) && !string.IsNullOrEmpty(order.Project))
        {
            order.ProjectName = await masterDataService.GetProjectNameAsync(order.Project, cancellationToken)
                ?? string.Empty;
        }

        return new SapInventoryGenExitRequestOrderLines
        {
            ProductionOrder = order,
            ProductionOrderLinesEntryNumber = [],
        };
    }

    public async Task<SapProductionOrdersResponse?> AddManualLineAsync(
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
        selection.ProductionOrderLinesEntryNumber ??= [];

        var maxLine = order.ProductionOrderLines.Max(x => x.LineNumber);
        line.LineNumber = maxLine + 1;
        line.ProductionOrderIssueType = "im_Manual";
        line.Project = order.Project;
        line.DocumentAbsoluteEntry = order.AbsoluteEntry;
        line.BaseQuantity = order.PlannedQuantity > 0
            ? line.PlannedQuantity / order.PlannedQuantity
            : 0;

        var item = await masterDataService.GetItemByCodeAsync(line.ItemNo ?? string.Empty, cancellationToken: cancellationToken);
        line.ItemName = item?.ItemName;

        var warehouse = await masterDataService.GetWarehouseByCodeAsync(line.Warehouse, cancellationToken: cancellationToken);
        line.LocationCode = warehouse?.Location ?? 0;

        order.ProductionOrderLines.Add(line);
        selection.ProductionOrderLinesEntryNumber.Add(line);

        var response = await productionOrdersService.UpdateProductionOrderAsync(order);
        if (response?.Error is not null && !string.IsNullOrEmpty(response.Error.Message?.Value))
        {
            order.ProductionOrderLines.Remove(line);
            selection.ProductionOrderLinesEntryNumber.Remove(line);
            throw new InvalidOperationException($"{response.Error.Code}: {response.Error.Message.Value}");
        }

        return response;
    }
}
