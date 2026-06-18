using SapApi.Modals.Enums.Pagination;

namespace SapApi.Modals.Pagination;

public class Filters<T> : PropertyName<T> where T : class, new()
{
    public dynamic? Value { get; set; } = null;
    public FilterType FilterType { get; set; } = FilterType.Equals;
    public FilterCondition FilterCondition { get; set; } = FilterCondition.Or;
    public List<Filters<T>>? SubFilters { get; set; } = null; 

}