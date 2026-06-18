using SapApi.Modals.Enums.Pagination;

namespace SapApi.Modals.Pagination;

public class Sorts<T> : PropertyName<T> where T : class, new()
{
    public SortType SortType { get; set; } = SortType.Desc;
}