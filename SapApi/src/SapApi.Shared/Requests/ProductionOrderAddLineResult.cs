using SapApi.Shared.Responses.Sap;

namespace SapApi.Shared.Requests;

/// <summary>
/// Result of adding a manual line to a SAP production order for Issue/Receipt forms.
/// </summary>
public class ProductionOrderAddLineResult
{
    public required SapProductionOrderLines AddedLine { get; set; }

    public SapProductionOrdersResponse? ProductionOrder { get; set; }
}
