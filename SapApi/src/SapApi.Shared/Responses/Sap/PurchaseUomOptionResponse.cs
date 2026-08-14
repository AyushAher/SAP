namespace SapApi.Shared.Responses.Sap;

/// <summary>
/// One unit the user may pick for a purchase line of a given item. Property names are pinned with
/// <see cref="JsonPropertyNameAttribute"/> so the UI contract does not depend on the API's naming policy.
/// </summary>
public record PurchaseUomOptionResponse
{
    /// <summary>UoM code as SAP shows it on the row (e.g. "KGS"). Goes to DocumentLines.MeasureUnit.</summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    /// <summary>UoM name (e.g. "KILOGRAMS"); falls back to the code when SAP has no name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// UoM AbsEntry — only set for units that come from the item's UoM group. Null means SAP has no
    /// group entry for this unit, so the caller must not send UoMCode/UoMEntry on the line.
    /// </summary>
    [JsonPropertyName("uomEntry")]
    public int? UoMEntry { get; init; }

    /// <summary>
    /// Inventory units per purchase unit (SAP NumPerMsr / UnitsOfMeasurment). Null when SAP does not
    /// define a conversion for this unit, i.e. the user must type the factor.
    /// </summary>
    [JsonPropertyName("itemsPerUnit")]
    public double? ItemsPerUnit { get; init; }

    /// <summary>True for the item's own purchase unit, so the UI can preselect it.</summary>
    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; init; }

    /// <summary>"group" when read from the item's UoM group, "master" when read from the UoM master.</summary>
    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;
}

/// <summary>Where a purchase-UoM option list came from.</summary>
public static class PurchaseUomOptionSources
{
    public const string Group = "group";
    public const string Master = "master";
}
