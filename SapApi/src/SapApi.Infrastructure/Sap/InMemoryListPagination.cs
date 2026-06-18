using SapApi.Shared.Models;

namespace SapApi.Infrastructure.Sap;

public static class InMemoryListPagination
{
    public static PaginationResponse<List<T>> Paginate<T>(
        IEnumerable<T> source,
        PaginationRequest request,
        Func<T, string, string?, bool>? filterMatcher = null)
    {
        var items = source.ToList();
        if (filterMatcher is not null)
        {
            items = items.Where(row =>
                request.Filters.All(filter =>
                {
                    if (filter.Value is null || string.IsNullOrWhiteSpace(filter.Value.ToString()))
                        return true;
                    return filterMatcher(row, filter.Field, filter.Value.ToString());
                })).ToList();
        }

        if (request.Sorts.Count > 0)
        {
            IOrderedEnumerable<T>? ordered = null;
            foreach (var sort in request.Sorts)
            {
                Func<T, object?> selector = row =>
                {
                    var property = typeof(T).GetProperty(sort.Field,
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.IgnoreCase);
                    return property?.GetValue(row);
                };

                ordered = ordered is null
                    ? (sort.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase)
                        ? items.OrderByDescending(selector)
                        : items.OrderBy(selector))
                    : (sort.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase)
                        ? ordered.ThenByDescending(selector)
                        : ordered.ThenBy(selector));
            }

            if (ordered is not null)
                items = ordered.ToList();
        }

        var pageSize = Math.Clamp(request.PageSize ?? 20, 1, 100);
        var skip = Math.Max(0, (request.PageNumber - 1) * pageSize);
        var page = items.Skip(skip).Take(pageSize).ToList();

        return PaginationResponseFactory.Create(request, page, items.Count);
    }
}
