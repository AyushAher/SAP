using System.Linq.Expressions;
using System.Reflection;
using SapApi.Shared.Models;

namespace SapApi.Infrastructure.Persistence;

public static class EfPaginationExtensions
{
    public static async Task<(List<T> Items, int TotalCount)> ToPaginatedListAsync<T>(
        this IQueryable<T> query,
        PaginationRequest request,
        CancellationToken cancellationToken = default)
        where T : class
    {
        query = ApplyFilters(query, request.Filters);
        var totalCount = await query.CountAsync(cancellationToken);
        query = ApplySorts(query, request.Sorts);

        var pageSize = Math.Clamp(request.PageSize ?? 20, 1, 100);
        var skip = Math.Max(0, (request.PageNumber - 1) * pageSize);

        var items = await query.Skip(skip).Take(pageSize).ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    private static IQueryable<T> ApplyFilters<T>(IQueryable<T> query, List<FilterModel> filters)
    {
        foreach (var filter in filters)
        {
            if (filter.Value is null || string.IsNullOrWhiteSpace(filter.Value.ToString()))
                continue;

            var property = typeof(T).GetProperty(filter.Field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null)
                continue;

            query = ApplyFilter(query, property, filter);
        }

        return query;
    }

    private static IQueryable<T> ApplyFilter<T>(IQueryable<T> query, PropertyInfo property, FilterModel filter)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var member = Expression.Property(parameter, property);
        var value = ConvertFilterValue(filter.Value, property.PropertyType);
        if (value is null)
            return query;

        Expression? predicate = filter.Operator.ToLowerInvariant() switch
        {
            "eq" => Expression.Equal(member, Expression.Constant(value, property.PropertyType)),
            "neq" => Expression.NotEqual(member, Expression.Constant(value, property.PropertyType)),
            "contains" when property.PropertyType == typeof(string) =>
                Expression.Call(member, nameof(string.Contains), Type.EmptyTypes, Expression.Constant(value.ToString())),
            "startswith" when property.PropertyType == typeof(string) =>
                Expression.Call(member, nameof(string.StartsWith), Type.EmptyTypes, Expression.Constant(value.ToString())),
            "endswith" when property.PropertyType == typeof(string) =>
                Expression.Call(member, nameof(string.EndsWith), Type.EmptyTypes, Expression.Constant(value.ToString())),
            "gt" => Expression.GreaterThan(member, Expression.Constant(value, property.PropertyType)),
            "gte" => Expression.GreaterThanOrEqual(member, Expression.Constant(value, property.PropertyType)),
            "lt" => Expression.LessThan(member, Expression.Constant(value, property.PropertyType)),
            "lte" => Expression.LessThanOrEqual(member, Expression.Constant(value, property.PropertyType)),
            _ when property.PropertyType == typeof(string) =>
                Expression.Call(member, nameof(string.Contains), Type.EmptyTypes, Expression.Constant(filter.Value.ToString())),
            _ => null,
        };

        if (predicate is null)
            return query;

        var lambda = Expression.Lambda<Func<T, bool>>(predicate, parameter);
        return query.Where(lambda);
    }

    private static object? ConvertFilterValue(object? raw, Type targetType)
    {
        if (raw is null)
            return null;

        var text = raw.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying == typeof(string))
            return text;

        if (underlying == typeof(int) && int.TryParse(text, out var intValue))
            return intValue;

        if (underlying == typeof(long) && long.TryParse(text, out var longValue))
            return longValue;

        if (underlying == typeof(bool) && bool.TryParse(text, out var boolValue))
            return boolValue;

        if (underlying == typeof(DateTime) && DateTime.TryParse(text, out var dateValue))
            return dateValue;

        if (underlying == typeof(decimal) && decimal.TryParse(text, out var decimalValue))
            return decimalValue;

        return text;
    }

    private static IQueryable<T> ApplySorts<T>(IQueryable<T> query, List<SortModel> sorts)
    {
        if (sorts.Count == 0)
            return query;

        IOrderedQueryable<T>? ordered = null;
        foreach (var sort in sorts)
        {
            var property = typeof(T).GetProperty(sort.Field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null)
                continue;

            var parameter = Expression.Parameter(typeof(T), "x");
            var member = Expression.Property(parameter, property);
            var keySelector = Expression.Lambda(member, parameter);
            var method = (ordered is null ? "OrderBy" : "ThenBy") +
                         (sort.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "Descending" : string.Empty);

            ordered = (IOrderedQueryable<T>)typeof(Queryable).GetMethods()
                .First(m => m.Name == method && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), property.PropertyType)
                .Invoke(null, [ordered ?? query, keySelector])!;
        }

        return ordered ?? query;
    }
}
