namespace SapApi.Shared.Models;

public record PaymentTermTypeOption
{
    public string Value { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// OPOR U_Tn payment-term type ValidValues helpers (SAP UserFieldsMD + app extras).
/// </summary>
public static class PaymentTermTypeOptions
{
    public const string GstProforma = "GstProforma";
    public const string TaxInvoice = "TaxInvoice";

    /// <summary>Types that store Payment% in U_Gn (GST) rather than U_Bn (Basic).</summary>
    public static readonly HashSet<string> GstMappedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        GstProforma,
        TaxInvoice,
    };

    public static readonly PaymentTermTypeOption[] AppExtras =
    [
        new() { Value = GstProforma, Description = "GST against Proforma Invoice" },
        new() { Value = TaxInvoice, Description = "Against Tax Invoice" },
    ];

    /// <summary>Used when SAP UserFieldsMD is unavailable.</summary>
    public static readonly PaymentTermTypeOption[] SapDefaults =
    [
        new() { Value = "Advance", Description = "As Advance" },
        new() { Value = "Proforma", Description = "Against Proforma" },
        new() { Value = "Invoice", Description = "Against Invoice" },
        new() { Value = "Retention", Description = "Retention" },
    ];

    public static bool IsGstMappedType(string? type) =>
        !string.IsNullOrWhiteSpace(type) && GstMappedTypes.Contains(type.Trim());

    /// <summary>
    /// Merges SAP ValidValues with app extras (extras appended only when Value is missing).
    /// </summary>
    public static List<PaymentTermTypeOption> MergeWithExtras(IEnumerable<PaymentTermTypeOption>? sapValues)
    {
        var result = new List<PaymentTermTypeOption>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in sapValues ?? [])
        {
            var value = item.Value?.Trim() ?? string.Empty;
            if (value.Length == 0 || !seen.Add(value))
                continue;
            result.Add(new PaymentTermTypeOption
            {
                Value = value,
                Description = string.IsNullOrWhiteSpace(item.Description) ? value : item.Description.Trim(),
            });
        }

        foreach (var extra in AppExtras)
        {
            if (!seen.Add(extra.Value))
                continue;
            result.Add(extra);
        }

        return result;
    }

    public static List<PaymentTermTypeOption> FallbackWithExtras() =>
        MergeWithExtras(SapDefaults);
}
