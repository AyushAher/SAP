namespace SapApi.Shared.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string errorCode, string message) =>
        new() { Success = false, ErrorCode = errorCode, Message = message };
}

public class PaginationRequest
{
    public int PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 20;
    public bool IncludeTotalCount { get; set; }
    public List<FilterModel> Filters { get; set; } = [];
    public List<SortModel> Sorts { get; set; } = [];

    public static PaginationRequest Normalize(PaginationRequest? request)
    {
        request ??= new PaginationRequest();
        request.Filters ??= [];
        request.Sorts ??= [];
        if (request.PageNumber < 1)
            request.PageNumber = 1;
        if (request.PageSize is null or < 1)
            request.PageSize = 20;
        return request;
    }
}

public class FilterModel
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "eq";
    public object? Value { get; set; }
}

public class SortModel
{
    public string Field { get; set; } = string.Empty;
    public string Direction { get; set; } = "asc";
}

public class PaginationResponse<T> : ApiResponse<T>
{
    public int PageNumber { get; set; }
    public int? PageSize { get; set; }
    public int? TotalCount { get; set; }
    public List<FilterModel> Filters { get; set; } = [];
    public List<SortModel> Sorts { get; set; } = [];
}
