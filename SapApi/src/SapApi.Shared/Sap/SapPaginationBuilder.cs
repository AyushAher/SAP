using System.Globalization;
using System.Text.Json;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Shared.Sap;

public static class SapPaginationBuilder
{
    public static SapQueries ToSapQueries(PaginationRequest request, SapPaginationOptions options)
    {
        var pageSize = Math.Clamp(request.PageSize ?? 20, 1, 100);
        var skip = Math.Max(0, (request.PageNumber - 1) * pageSize);

        var filterParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.BaseFilter))
            filterParts.Add($"({options.BaseFilter.Trim()})");

        foreach (var filter in request.Filters)
        {
            if (filter.Value is null || string.IsNullOrWhiteSpace(filter.Value.ToString()))
                continue;

            if (filter.Field.Equals("__search", StringComparison.OrdinalIgnoreCase))
            {
                var searchClause = BuildSearchFilter(filter.Value.ToString()!.Trim(), options);
                if (!string.IsNullOrWhiteSpace(searchClause))
                    filterParts.Add(searchClause);
                continue;
            }

            var sapField = options.FieldMap.TryGetValue(filter.Field, out var mapped)
                ? mapped
                : filter.Field;

            filterParts.Add(BuildFilterClause(sapField, filter));
        }

        var orderBy = BuildOrderBy(request, options);

        return new SapQueries
        {
            Filter = filterParts.Count > 0 ? string.Join(" and ", filterParts) : null,
            OrderBy = orderBy,
            Select = options.Select,
            Skip = skip.ToString(CultureInfo.InvariantCulture),
            Top = pageSize.ToString(CultureInfo.InvariantCulture),
            InlineCount = request.IncludeTotalCount,
        };
    }

    public static string EscapeODataString(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);

    public static bool LooksLikeMasterCode(string search) =>
        !string.IsNullOrWhiteSpace(search)
        && search.Length <= 50
        && !search.Contains(' ')
        && search.All(static c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.');

    public static int ResolveTotalCountFromItems<T>(IReadOnlyList<T>? items, PaginationRequest request)
    {
        var pageSize = request.PageSize ?? 20;
        var skip = Math.Max(0, (request.PageNumber - 1) * pageSize);
        var count = items?.Count ?? 0;

        if (count == 0)
            return skip;

        if (count < pageSize)
            return skip + count;

        return skip + count + 1;
    }

    public static int ResolveTotalCount<T>(SapBaseResponse response, IReadOnlyList<T>? items, PaginationRequest request)
    {
        var explicitCount = response.GetTotalCount();
        if (explicitCount.HasValue)
            return (int)Math.Min(explicitCount.Value, int.MaxValue);

        return ResolveTotalCountFromItems(items, request);
    }

    private static string BuildSearchFilter(string search, SapPaginationOptions options)
    {
        var escaped = EscapeODataString(search);
        var clauses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var codeFields = options.SearchCodeFields.Count > 0
            ? options.SearchCodeFields
            : options.SearchOrFields.Take(1).ToList();
        var textFields = options.SearchTextFields.Count > 0
            ? options.SearchTextFields
            : options.SearchOrFields.Skip(codeFields.Count).ToList();

        var exactCode = LooksLikeMasterCode(search);

        foreach (var field in codeFields)
        {
            if (exactCode)
                clauses.Add($"{field} eq '{escaped}'");
            else if (escaped.Length >= 1)
                clauses.Add($"startswith({field},'{escaped}')");
        }

        foreach (var field in textFields)
        {
            if (exactCode)
            {
                if (escaped.Length >= 1)
                    clauses.Add($"startswith({field},'{escaped}')");
            }
            else if (escaped.Length >= 3)
                clauses.Add($"contains({field},'{escaped}')");
            else if (escaped.Length >= 1)
                clauses.Add($"startswith({field},'{escaped}')");
        }

        if (clauses.Count == 0 && options.SearchOrFields.Count > 0)
        {
            foreach (var field in options.SearchOrFields)
            {
                if (exactCode)
                    clauses.Add($"{field} eq '{escaped}'");
                else
                    clauses.Add($"startswith({field},'{escaped}')");
            }
        }

        return clauses.Count == 0 ? string.Empty : $"({string.Join(" or ", clauses)})";
    }

    private static string BuildOrderBy(PaginationRequest request, SapPaginationOptions options)
    {
        if (request.Sorts.Count > 0)
        {
            return string.Join(",",
                request.Sorts.Select(sort =>
                {
                    var field = options.FieldMap.TryGetValue(sort.Field, out var mapped)
                        ? mapped
                        : sort.Field;
                    var direction = sort.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
                    return $"{field} {direction}";
                }));
        }

        var defaultField = options.FieldMap.TryGetValue(options.DefaultSortField, out var defaultMapped)
            ? defaultMapped
            : options.DefaultSortField;
        var defaultDirection = options.DefaultSortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
        return $"{defaultField} {defaultDirection}";
    }

    private static string BuildFilterClause(string sapField, FilterModel filter)
    {
        var value = UnwrapJsonValue(filter.Value);
        var formatted = FormatODataValue(value);
        var operatorName = filter.Operator.ToLowerInvariant();

        if (operatorName is "contains" or "startswith" or "endswith")
        {
            var text = value?.ToString()?.Trim();
            if (string.IsNullOrEmpty(text))
                return $"{sapField} eq {formatted}";

            if (operatorName == "contains" && LooksLikeMasterCode(text) && IsLikelyCodeField(sapField))
                return $"{sapField} eq {formatted}";
        }

        return operatorName switch
        {
            "eq" => $"{sapField} eq {formatted}",
            "neq" => $"{sapField} ne {formatted}",
            "contains" => IsLikelyCodeField(sapField)
                ? $"contains({sapField},{formatted})"
                : $"startswith({sapField},{formatted})",
            "startswith" => $"startswith({sapField},{formatted})",
            "endswith" => $"endswith({sapField},{formatted})",
            "gt" => $"{sapField} gt {formatted}",
            "gte" => $"{sapField} ge {formatted}",
            "lt" => $"{sapField} lt {formatted}",
            "lte" => $"{sapField} le {formatted}",
            _ => LooksLikeMasterCode(value?.ToString() ?? string.Empty) && IsLikelyCodeField(sapField)
                ? $"{sapField} eq {formatted}"
                : $"startswith({sapField},{formatted})",
        };
    }

    private static bool IsLikelyCodeField(string sapField) =>
        sapField.EndsWith("Code", StringComparison.OrdinalIgnoreCase)
        || sapField.Equals("DocNum", StringComparison.OrdinalIgnoreCase)
        || sapField.Equals("NumAtCard", StringComparison.OrdinalIgnoreCase)
        || sapField.Equals("BPLID", StringComparison.OrdinalIgnoreCase)
        || sapField.Equals("Code", StringComparison.OrdinalIgnoreCase);

    private static object? UnwrapJsonValue(object? value) =>
        value is JsonElement element ? JsonElementToObject(element) : value;

    private static object? JsonElementToObject(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString(),
        };

    private static string FormatODataValue(object? value)
    {
        if (value is null)
            return "null";

        if (value is bool b)
            return b ? "true" : "false";

        if (value is int or long or short or byte or decimal or double or float)
            return Convert.ToString(value, CultureInfo.InvariantCulture)!;

        if (value is DateTime dt)
            return $"'{dt:yyyy-MM-dd}'";

        if (DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            return $"'{parsedDate:yyyy-MM-dd}'";

        var text = value.ToString()?.Replace("'", "''", StringComparison.Ordinal) ?? string.Empty;
        return $"'{text}'";
    }
}
