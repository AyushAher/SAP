using Shared.Modals.Pagination;

namespace SapApi.Modals.Pagination
{
    public class PaginationResponseModal<TEntity, TResult> : ApiResponseModal
        where TEntity : class, new()
        where TResult : class, new()
    {
        public List<TResult> Data { get; set; } = [];
        public int? CurrentPage { get; set; } = 1;
        public int? PageSize { get; set; } = 10;
        public List<Filters<TEntity>>? Filters { get; set; }
        public List<Sorts<TEntity>>? Sorts { get; set; }
        public long? TotalCount { get; set; }

        public PaginationResponseModal(bool succeeded, string? errorCode = null, string? message = null)
            : base(succeeded, errorCode, message)
        {
        }

        public PaginationResponseModal(bool succeeded, (IEnumerable<TResult> data, int count) param,
            PaginationRequestModal<TEntity> requestModal,
            string? errorCode = null, string? message = null) : base(succeeded, errorCode, message)
        {
            Data = param.data.ToList();
            Filters = requestModal.Filters;
            Sorts = requestModal.Sorts;
            CurrentPage = requestModal.CurrentPage;
            PageSize = requestModal.PageSize;
            TotalCount = param.count;
        }

        public static PaginationResponseModal<TEntity, TResult> Success(PaginationRequestModal<TEntity> requestModal,
            (IEnumerable<TResult> data, int count) param)
            => new(true, param, requestModal);

    }
}