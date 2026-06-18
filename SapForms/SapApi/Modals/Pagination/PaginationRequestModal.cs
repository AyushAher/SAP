using SapApi.Modals.Enums.Pagination;
using SapApi.Modals.Pagination;
using System.Globalization;
using System.Text.Json;
using SapApi;
using SapApi.Modals;

namespace Shared.Modals.Pagination
{
    public partial class PaginationRequestModal<T> where T : class, new()
    {
        public int? CurrentPage { get; set; } = 1;
        public int? PageSize { get; set; } = 10;
        public List<Filters<T>>? Filters { get; set; } = null;
        public List<Sorts<T>>? Sorts { get; set; } = null;

        public (string query, string countQuery, Dictionary<string, dynamic?> parameters) BuildSql(string columns,
            bool isCaseSensitive = false, bool appendGroupBy = false)
        {
            var tableName = new T().GetTable();
            (string query, Dictionary<string, dynamic?> parameters) filterResult = Filter(isCaseSensitive);
            Dictionary<string, dynamic?> deserializedParameters = TransferFromJsonElementParameters(filterResult.parameters);
            var query =
                $"""
                SELECT {columns} 
                FROM {tableName}
                {filterResult.query}
                {(appendGroupBy ? GetGroupBy(columns) : "")}
                {Sort(isCaseSensitive)}
                """;

            var countQuery = $"SELECT COUNT(*) FROM ({query}) AS CountQuery";
            var paginatedQuery = query + Paginate();

            return (paginatedQuery, countQuery, deserializedParameters);
        }

        private string Paginate()
            => PageSize.HasValue ? $" LIMIT {PageSize.Value} OFFSET {((CurrentPage ?? 1) - 1) * PageSize.Value}" : "";

        private string Sort(bool isCaseSensitive)
        {
            if (Sorts is null or { Count: 0 }) return "";

            string QuoteColumn(string col) => $"\"{col.Trim('"')}\"";

            var sortQuery = Sorts.Aggregate("ORDER BY ",
                (current, sort) =>
                {
                    var column = sort.ColumnName(isCaseSensitive);
                    var quotedColumn = QuoteColumn(column);
                    return current + $" {quotedColumn} {GetSortType(sort.SortType)},";
                });

            return sortQuery.TrimEnd(',');
        }

        private (string query, Dictionary<string, dynamic?> parameters) Filter(bool isCaseSensitive)
        {
            if (Filters is null or { Count: 0 }) return ("", new Dictionary<string, dynamic?>());

            var filterObject = new Dictionary<string, dynamic?>();
            var condition = BuildFilterCondition(Filters, filterObject, isCaseSensitive);

            return (condition, filterObject);
        }

        private static string BuildFilterCondition(
            IReadOnlyList<Filters<T>> filters,
            IDictionary<string, dynamic?> filterObject,
            bool isCaseSensitive,
            int depth = 0)
        {
            var condition = "";
            string QuoteColumn(string col) => $"\"{col.Trim('"')}\"";

            if (depth == 0)
            {
                condition = "WHERE ";
            }

            for (var index = 0; index < filters.Count; index++)
            {
                Filters<T> filter = filters[index];
                var valueParameter = $"@fil_{filter.Property}_value_{index}";
                var column = filter.ColumnName(isCaseSensitive);

                filterObject[valueParameter] = filter.Value;
                if (index > 0)
                {
                    condition += $" {GetFilterCondition(filter.FilterCondition, index)} ";
                }

                if (filter.SubFilters != null && filter.SubFilters.Any())
                {
                    condition += $"({BuildFilterCondition(filter.SubFilters, filterObject, isCaseSensitive, depth + 1)})";
                }
                else
                {
                    dynamic? paramValue = filterObject[valueParameter];

                    var isJsonString =
                     paramValue is JsonElement je && je.ValueKind == JsonValueKind.String;

                    var isJsonNumber =
                        paramValue is JsonElement jeNum && jeNum.ValueKind == JsonValueKind.Number;

                    var isDate =
                        paramValue is DateTime ||
                        (paramValue is JsonElement jeDate &&
                            (jeDate.ValueKind == JsonValueKind.String || jeDate.ValueKind == JsonValueKind.Object) &&
                            DateTime.TryParse(jeDate.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _));

                    var isInteger =
                        paramValue is int || paramValue is long || paramValue is short || paramValue is byte ||
                        (isJsonNumber && ((JsonElement)paramValue).TryGetInt64(out _));

                    var needsLower =
                        !isCaseSensitive &&
                        !isDate &&
                        !isInteger &&
                        (paramValue is string || isJsonString);

                    var quotedColumn = QuoteColumn(column);

                    var columnExpression = quotedColumn;

                    if (needsLower)
                    {
                        columnExpression = $"LOWER({quotedColumn})";
                    }

                    condition += GetFilterTypeOperator(filter.FilterType, columnExpression, valueParameter, needsLower);
                }

            }

            return condition;
        }

        private static string GetFilterTypeOperator(FilterType filterType, string columnExpression, string parameterName, bool lower)
        {
            return filterType switch
            {
                FilterType.Equals => $"{columnExpression} = {(lower ? $"LOWER({parameterName})" : parameterName)}",
                FilterType.NotEquals => $"{columnExpression} <> {(lower ? $"LOWER({parameterName})" : parameterName)}",
                FilterType.Contains => $"{columnExpression} LIKE {(lower ? $"LOWER(CONCAT('%', {parameterName}, '%'))" : $"CONCAT('%', {parameterName}, '%')")}",
                FilterType.NotContains => $"{columnExpression} NOT LIKE {(lower ? $"LOWER(CONCAT('%', {parameterName}, '%'))" : $"CONCAT('%', {parameterName}, '%')")}",
                FilterType.GreaterThan => $"{columnExpression} > {parameterName}",
                FilterType.LessThan => $"{columnExpression} < {parameterName}",
                FilterType.IsNotNull => $"{columnExpression} IS NOT NULL",
                FilterType.IsNull => $"{columnExpression} IS NULL",
                FilterType.GreaterThanOrEqual => $"{columnExpression} >= {parameterName}",
                _ => throw new NotSupportedException($"Unsupported filter type: {filterType}")
            };
        }

        private static string GetFilterCondition(FilterCondition condition, int index)
        {
            return index > 0 ? (condition == FilterCondition.Or ? "OR" : "AND") : "";
        }

        private static string GetSortType(SortType sortType)
        {
            return sortType == SortType.Desc ? "DESC" : "ASC";
        }

        private static string GetGroupBy(string columns)
        {
            if (string.IsNullOrWhiteSpace(columns) || columns.Trim() == "*")
                return "";

            //var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            //    .Select(prop => @"""" + prop.Name + @"""");

            IEnumerable<string> properties = columns.Split(',')
                .Select(col => col.Trim())
                .Where(col => !string.IsNullOrWhiteSpace(col))
                .Distinct();

            return "GROUP BY " + string.Join(", ", properties);
        }
        /// <summary>
        /// Converts the Parameters 
        /// </summary>
        /// <param name="parameters">Values of the filters which datatype</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private Dictionary<string, dynamic?> TransferFromJsonElementParameters(Dictionary<string, dynamic?> parameters)
        {
            var keys = parameters.Keys.ToList();
            foreach (var key in keys)
            {
                if (parameters[key] is JsonElement jsonElement)
                {
                    parameters[key] = jsonElement.ValueKind switch
                    {
                        JsonValueKind.Number => jsonElement.TryGetInt64(out var longValue) ? longValue : jsonElement.GetDouble(),
                        JsonValueKind.String => ParseString(jsonElement.GetString()),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => throw new ApiErrorException(BaseErrorCodes.SystemError, "Some error occurred while fetching records")
                    };
                }
            }
            return parameters;
        }

        /// <summary>
        /// Check if the value is date or string.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static object? ParseString(string? str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return str;

            // Try parse as DateTime
            if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateValue))
                return dateValue;
            // If not a date, return as string
            return str;
        }
    }
}