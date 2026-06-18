using SapApi.Shared.Models;

namespace SapApi.Infrastructure.Sap;

public static class PaginationResponseFactory
{
    public static PaginationResponse<TData> Create<TData>(
        PaginationRequest request,
        TData data,
        int totalCount)
    {
        var pageSize = request.PageSize ?? 20;
        return new PaginationResponse<TData>
        {
            Success = true,
            Data = data,
            PageNumber = request.PageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            Filters = request.Filters,
            Sorts = request.Sorts,
        };
    }
}
