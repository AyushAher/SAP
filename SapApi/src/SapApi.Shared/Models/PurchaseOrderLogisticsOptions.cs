using System.Text.Json;
using System.Text.RegularExpressions;

namespace SapApi.Shared.Models;

/// <summary>ValidValues option from SAP UserFieldsMD (value + description).</summary>
public record SapUdfValidValueOption
{
    public string Value { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

/// <summary>A marketing-document UDF plus its SAP ValidValues (Name without the U_ prefix).</summary>
public record SapUdfFieldOptions
{
    public string Field { get; init; } = string.Empty;
    public List<SapUdfValidValueOption> Options { get; init; } = [];
}

/// <summary>PO Logistics / Other Terms dropdown options sourced from SAP UDFs.</summary>
public record PurchaseOrderLogisticsOptions
{
    /// <summary>ADOC/OPOR U_PRI_BAS ValidValues.</summary>
    public List<SapUdfValidValueOption> PriceBasis { get; init; } = [];

    /// <summary>ADOC/OPOR U_TransMode ValidValues.</summary>
    public List<SapUdfValidValueOption> ModeOfTransport { get; init; } = [];

    /// <summary>ADOC/OPOR U_UN_LOAD ValidValues.</summary>
    public List<SapUdfValidValueOption> Unloading { get; init; } = [];

    /// <summary>ADOC/OPOR U_TRANS ValidValues.</summary>
    public List<SapUdfValidValueOption> Transportation { get; init; } = [];

    /// <summary>ADOC/OPOR U_TRANINSU ValidValues.</summary>
    public List<SapUdfValidValueOption> TransitInsurance { get; init; } = [];

    /// <summary>Packing Forwarding UDF (name discovered from UserFieldsMD) + ValidValues.</summary>
    public SapUdfFieldOptions? PackingForwarding { get; init; }

    /// <summary>TC Dispatch Address UDF (name discovered from UserFieldsMD) + ValidValues.</summary>
    public SapUdfFieldOptions? TcDispatchAddress { get; init; }
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

    /// <summary>PBBPL ADOC/OPOR U_UN_LOAD / U_TRANINSU ValidValues.</summary>
    public static readonly SapUdfValidValueOption[] ScopeOrNotApplicable =
    [
        new() { Value = "IN OUR SCOPE", Description = "IN OUR SCOPE" },
        new() { Value = "IN YOUR SCOPE", Description = "IN YOUR SCOPE" },
        new() { Value = "NOT APPLY", Description = "NOT APPLICABLE" },
    ];

    /// <summary>PBBPL ADOC/OPOR U_TRANS ValidValues (no Not Applicable).</summary>
    public static readonly SapUdfValidValueOption[] Transportation =
    [
        new() { Value = "IN OUR SCOPE", Description = "IN OUR SCOPE" },
        new() { Value = "IN YOUR SCOPE", Description = "IN YOUR SCOPE" },
    ];

    /// <summary>PBBPL ADOC/OPOR U_PAC_FOR ValidValues (SAP value is misspelled NOT APLY).</summary>
    public static readonly SapUdfValidValueOption[] PackingForwarding =
    [
        new() { Value = "IN OUR SCOPE", Description = "IN OUR SCOPE" },
        new() { Value = "IN YOUR SCOPE", Description = "IN YOUR SCOPE" },
        new() { Value = "NOT APLY", Description = "NOT APPLICABLE" },
    ];

    /// <summary>PBBPL ADOC/OPOR U_TCDISADD ValidValues (SAP includes a misspelled FAXTORY row).</summary>
    public static readonly SapUdfValidValueOption[] TcDispatchAddress =
    [
        new() { Value = "SUPA FAXTORY ADDRESS", Description = "SUPA FAXTORY ADDRESS" },
        new() { Value = "H.O. ADDRESS", Description = "H.O. ADDRESS" },
        new() { Value = "NOT APPLY", Description = "NOT APPLICABLE" },
        new() { Value = "SUPA FACTORY ADDRESS", Description = "SUPA FACTORY ADDRESS" },
    ];
}

/// <summary>
/// OPOR/ADOC Other Terms UDFs that are ValidValues dropdowns in SAP.
/// Packing Forwarding and TC Dispatch Address names vary by company DB — match by Name then Description.
/// </summary>
public static partial class PurchaseOrderOtherTermUdf
{
    public const string Unloading = "UN_LOAD";
    public const string Transportation = "TRANS";
    public const string TransitInsurance = "TRANINSU";
    public const string PackingForwarding = "PAC_FOR";
    public const string TcDispatchAddress = "TCDISADD";

    public static readonly string[] PackingForwardingCandidates =
    [
        PackingForwarding, "PACK_FOR", "PACKFORW", "PACK_FWD", "PACKFWD", "PCKFWD", "PCK_FWD",
        "PKGFWD", "PACKING", "PACK_FRW", "PNF",
    ];

    public static readonly string[] TcDispatchAddressCandidates =
    [
        TcDispatchAddress, "TC_DISP", "TCDISP", "TC_DISPAD", "TCADD", "TC_ADD",
        "TCDISPADD", "TC_DISADD", "TCDISPATCH", "DISP_TC",
    ];

    public static bool IsPackingForwarding(string? name, string? description)
    {
        var n = (name ?? string.Empty).Trim();
        if (n.Length > 0
            && PackingForwardingCandidates.Contains(n, StringComparer.OrdinalIgnoreCase))
            return true;

        var d = (description ?? string.Empty).ToLowerInvariant();
        if (d.Contains("p&f", StringComparison.Ordinal)
            || d.Contains("p & f", StringComparison.Ordinal)
            || d.Contains("p and f", StringComparison.Ordinal))
            return true;

        return d.Contains("pack", StringComparison.Ordinal)
            && d.Contains("forward", StringComparison.Ordinal);
    }

    public static bool IsTcDispatchAddress(string? name, string? description)
    {
        var n = (name ?? string.Empty).Trim();
        if (n.Length > 0
            && TcDispatchAddressCandidates.Contains(n, StringComparer.OrdinalIgnoreCase))
            return true;

        var d = (description ?? string.Empty).ToLowerInvariant();
        var hasTc = ContainsWord(d, "tc") || d.Contains("test cert", StringComparison.Ordinal);
        var hasDispatch = d.Contains("dispatch", StringComparison.Ordinal)
            || d.Contains("dispach", StringComparison.Ordinal);
        return hasTc && hasDispatch;
    }

    public static bool IsWritableAdditionalKey(string? key, JsonElement value = default)
    {
        if (string.IsNullOrWhiteSpace(key)
            || !key.StartsWith("U_", StringComparison.OrdinalIgnoreCase))
            return false;
        if (value.ValueKind is JsonValueKind.Undefined)
            return true;
        return value.ValueKind is JsonValueKind.String or JsonValueKind.Number
            or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;
    }

    public static Dictionary<string, JsonElement>? CopyWritableAdditionalUdf(
        Dictionary<string, JsonElement>? source)
    {
        if (source is null || source.Count == 0)
            return null;

        Dictionary<string, JsonElement>? copy = null;
        foreach (var (key, value) in source)
        {
            if (!IsWritableAdditionalKey(key, value))
                continue;
            copy ??= new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            copy[key] = value;
        }

        return copy is { Count: > 0 } ? copy : null;
    }

    public static Dictionary<string, JsonElement>? MergeWritableAdditionalUdf(
        Dictionary<string, JsonElement>? target,
        Dictionary<string, JsonElement>? source)
    {
        var copy = CopyWritableAdditionalUdf(target);
        if (source is null || source.Count == 0)
            return copy;

        foreach (var (key, value) in source)
        {
            if (!IsWritableAdditionalKey(key, value))
                continue;
            copy ??= new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            copy[key] = value;
        }

        return copy is { Count: > 0 } ? copy : null;
    }

    [GeneratedRegex(@"\btc\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TcWordRegex();

    private static bool ContainsWord(string text, string word) =>
        word.Equals("tc", StringComparison.OrdinalIgnoreCase) && TcWordRegex().IsMatch(text);
}
