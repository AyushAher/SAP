using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using SapApi.Infrastructure.Jobs;
using SapApi.Shared.Configuration;

namespace SapApi.Api.Hangfire;

public static class HangfireExtensions
{
    public const string MasterDataRefreshJobId = MasterDataCacheRefreshJob.RecurringJobId;

    public static IServiceCollection AddSapHangfire(this IServiceCollection services, IConfiguration configuration)
    {
        var hangfireSection = configuration.GetSection(HangfireOptions.Label);
        services.Configure<HangfireOptions>(hangfireSection);

        var options = hangfireSection.Get<HangfireOptions>() ?? new HangfireOptions();
        var useInMemory = configuration.GetValue<bool>("Testing:UseInMemoryDatabase");
        if (!options.Enabled || useInMemory)
            return services;

        var connectionString = configuration.GetConnectionString("DbConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DbConnection is required for Hangfire.");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString), new PostgreSqlStorageOptions
            {
                // Shared schema keeps Hangfire tables isolated from EF app tables.
                SchemaName = "hangfire",
                PrepareSchemaIfNecessary = true,
            }));

        services.AddHangfireServer(serverOptions =>
        {
            serverOptions.WorkerCount = Math.Max(1, Environment.ProcessorCount / 2);
            serverOptions.Queues = ["default"];
        });

        services.AddScoped<MasterDataCacheRefreshJob>();
        return services;
    }

    public static WebApplication UseSapHangfire(this WebApplication app)
    {
        var options = app.Configuration.GetSection(HangfireOptions.Label).Get<HangfireOptions>() ?? new HangfireOptions();
        var useInMemory = app.Configuration.GetValue<bool>("Testing:UseInMemoryDatabase");
        if (!options.Enabled || useInMemory)
            return app;

        if (!string.IsNullOrWhiteSpace(options.DashboardPath))
        {
            app.UseHangfireDashboard(options.DashboardPath, new DashboardOptions
            {
                Authorization = [new HangfireDashboardAuthorizationFilter(app.Environment)],
                DashboardTitle = "SAP Platform Jobs",
            });
        }

        RecurringJob.AddOrUpdate<MasterDataCacheRefreshJob>(
            MasterDataRefreshJobId,
            job => job.ExecuteAsync(CancellationToken.None),
            options.MasterDataRefreshCron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // Warm immediately on startup so the first request after deploy doesn't wait for the cron.
        BackgroundJob.Enqueue<MasterDataCacheRefreshJob>(job => job.ExecuteAsync(CancellationToken.None));

        return app;
    }
}

/// <summary>
/// Hangfire dashboard auth. Development: local requests only. Production: Admin/SuperAdmin JWT cookie
/// or Authorization header (browser JWT apps typically won't have a cookie — prefer tunnel/local access).
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter(IHostEnvironment environment) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        if (environment.IsDevelopment())
            return http.Connection.RemoteIpAddress is null
                || http.Connection.RemoteIpAddress.Equals(http.Connection.LocalIpAddress)
                || System.Net.IPAddress.IsLoopback(http.Connection.RemoteIpAddress);

        return http.User.Identity?.IsAuthenticated == true
            && (http.User.IsInRole("Admin") || http.User.IsInRole("SuperAdmin"));
    }
}
