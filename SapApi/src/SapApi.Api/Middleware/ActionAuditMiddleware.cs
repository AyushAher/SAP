using System.Diagnostics;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Audit;
using SapApi.Infrastructure.Identity;

namespace SapApi.Api.Middleware;

public class ActionAuditMiddleware(RequestDelegate next, ILogger<ActionAuditMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        IHttpContextAccessor httpContextAccessor,
        IServiceScopeFactory scopeFactory)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;
        if (!ActionAuditNaming.ShouldAudit(method, path))
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        await next(context);
        stopwatch.Stop();

        var userId = httpContextAccessor.GetUserIdAsync();
        var userName = httpContextAccessor.GetUserName();
        var companyDb = httpContextAccessor.GetCompanyDb()?.ToString();
        var action = ActionAuditNaming.BuildActionLabel(method, path);
        var statusCode = context.Response.StatusCode;
        var ipAddress = ResolveClientIp(context);
        var durationMs = (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds);

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var auditService = scope.ServiceProvider.GetRequiredService<IActionAuditLogService>();
                await auditService.RecordAsync(new ActionAuditEntry(
                    userId,
                    userName,
                    companyDb,
                    method.ToUpperInvariant(),
                    path,
                    action,
                    statusCode,
                    ipAddress,
                    durationMs));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to persist action audit log for {Action}", action);
            }
        });
    }

    private static string? ResolveClientIp(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.MapToIPv4().ToString();
    }
}
