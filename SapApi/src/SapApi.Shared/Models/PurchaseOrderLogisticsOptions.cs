namespace SapApi.Shared.Models;

/// <summary>ValidValues option from SAP UserFieldsMD (value + description).</summary>
public record SapUdfValidValueOption
{
    public string Value { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

/// <summary>PO Logistics dropdown options sourced from SAP UDFs / masters.</summary>
public record PurchaseOrderLogisticsOptions
{
    /// <summary>ADOC/OPOR U_PRI_BAS ValidValues.</summary>
    public List<SapUdfValidValueOption> PriceBasis { get; init; } = [];

    /// <summary>ADOC/OPOR U_TransMode ValidValues.</summary>
    public List<SapUdfValidValueOption> ModeOfTransport { get; init; } = [];
}

/// <summary>Fallbacks when UserFieldsMD is unavailable (match PBBPL ValidValues).</summary>
public static class PurchaseOrderLogisticsOptionDefaults
{
    public static readonly SapUdfValidValueOption[] PriceBasis =
    [
        new() { Value = "ex works(incoterms)", Description = "ex works(incoterms)" },
        new() { Value = "F.O.R.", Description = "F.O.R." },
        new() { Value = "NOT APPLIC", Description = "NOT APPLICABLE" },
    ];

    public static readonly SapUdfValidValueOption[] ModeOfTransport =
    [
        new() { Value = "-", Description = "Not Applicable" },
        new() { Value = "1", Description = "Road" },
        new() { Value = "2", Description = "Rail" },
        new() { Value = "3", Description = "Air" },
        new() { Value = "4", Description = "Ship" },
    ];
}
