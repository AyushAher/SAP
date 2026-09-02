namespace SapApi.Shared.Sap;

/// <summary>
/// Portal sub-assemblies are isolated in Postgres and tagged on the parent SAP order's
/// component lines (WOR1 U_DocNum, description "Subassembly"). Parent BOM lines have no tag.
/// The portal number is <c>{parent}/{sequence}</c>; Service Layer rejects a slash in this
/// UDF (Error -1) but accepts a hyphen, so SAP is written as <c>{parent}-{sequence}</c>.
/// </summary>
public static class ProductionOrderSubassemblyTag
{
    public static bool IsTagged(string? docNum) => Parse(docNum) is not null;

    public static bool EqualsTag(string? docNum, string? tag)
    {
        var left = Parse(docNum);
        var right = Parse(tag);
        if (left is not null && right is not null)
        {
            if (!string.Equals(left.Value.Sequence, right.Value.Sequence, StringComparison.Ordinal))
                return false;
            if (string.Equals(left.Value.Parent, right.Value.Parent, StringComparison.OrdinalIgnoreCase))
                return true;
            // Create-with-subassemblies tags WOR1 as 0-n until SAP assigns DocumentNumber.
            return left.Value.Parent == "0" || right.Value.Parent == "0";
        }

        var sapLeft = ToSapTag(docNum);
        var sapRight = ToSapTag(tag);
        return sapLeft is not null
            && sapRight is not null
            && string.Equals(sapLeft, sapRight, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// WOR1 value Service Layer will accept. Portal <c>8/1</c> becomes <c>8-1</c>.
    /// </summary>
    public static string? ToSapTag(string? portalTag)
    {
        var parsed = Parse(portalTag);
        return parsed is null ? NullIfBlank(portalTag)?.Replace('/', '-') : parsed.Value.Sap;
    }

    /// <summary>
    /// Portal sub-assembly number with a new parent DocumentNumber, keeping the sequence
    /// (<c>0/1</c> + parent 35 → <c>35/1</c>).
    /// </summary>
    public static string? WithParent(string? tag, string? parentDocumentNumber)
    {
        var parent = NullIfBlank(parentDocumentNumber);
        var parsed = Parse(tag);
        if (parent is null || parsed is null)
            return NullIfBlank(tag);
        return parent + "/" + parsed.Value.Sequence;
    }

    /// <summary>
    /// Parent DocumentNumber from a stored sub-assembly no (<c>13/2</c> or <c>13-2</c> → <c>13</c>,
    /// legacy <c>13</c> → <c>13</c>).
    /// </summary>
    public static string? ParentDocumentNumber(string? subassemblyNo)
    {
        var parsed = Parse(subassemblyNo);
        if (parsed is not null)
            return parsed.Value.Parent;

        return NullIfBlank(subassemblyNo);
    }

    static (string Parent, string Sequence, string Sap)? Parse(string? value)
    {
        var raw = NullIfBlank(value);
        if (raw is null)
            return null;

        var sep = raw.LastIndexOfAny(['/', '-']);
        if (sep <= 0 || sep >= raw.Length - 1)
            return null;

        var parent = raw[..sep];
        var sequence = raw[(sep + 1)..];
        if (!IsDigits(parent) || !IsDigits(sequence))
            return null;

        return (parent, sequence, parent + "-" + sequence);
    }

    static bool IsDigits(string value)
    {
        foreach (var ch in value)
        {
            if (!char.IsDigit(ch))
                return false;
        }

        return value.Length > 0;
    }

    static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
