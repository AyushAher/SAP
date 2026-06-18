namespace SapApi.Shared.Sap;

public class SapPaginationOptions
{
    public string? BaseFilter { get; init; }
    public string? Select { get; init; }
    public Dictionary<string, string> FieldMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string DefaultSortField { get; init; } = "DocEntry";
    public string DefaultSortDirection { get; init; } = "asc";
    public IReadOnlyList<string> SearchOrFields { get; init; } = [];
    /// <summary>Fields matched with eq/startswith during typeahead (e.g. ItemCode, CardCode).</summary>
    public IReadOnlyList<string> SearchCodeFields { get; init; } = [];
    /// <summary>Fields matched with startswith/contains during typeahead (e.g. ItemName, CardName).</summary>
    public IReadOnlyList<string> SearchTextFields { get; init; } = [];
}
