using SapApi.Shared;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Models;

namespace SapApi.Api.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiErrorException ex)
        {
            logger.LogWarning(ex, "API error: {Code}", ex.ErrorCode);
            context.Response.StatusCode = ex.ErrorCode == BaseErrorCodes.IncorrectCredentials
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(ex.ErrorCode, ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("SYS-01", "An unexpected error occurred"));
        }
    }
}
