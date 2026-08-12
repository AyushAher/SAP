using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SapApi.Infrastructure;
using SapApi.PoSync;
using Serilog;

return await PurchaseOrderSyncHost.RunAsync(args);

internal static class PurchaseOrderSyncHost
{
    public static async Task<int> RunAsync(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            var builder = Host.CreateApplicationBuilder(args);
            HostEnvironmentBootstrap.Apply(builder.Configuration);
            ApplyCliOverrides(builder.Configuration, args);

            builder.Services.AddSerilog((_, cfg) =>
                cfg.ReadFrom.Configuration(builder.Configuration).WriteTo.Console());

            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.Configure<PurchaseOrderSyncOptions>(
                builder.Configuration.GetSection(PurchaseOrderSyncOptions.Label));
            builder.Services.Configure<ProductionOrderSyncOptions>(
                builder.Configuration.GetSection(ProductionOrderSyncOptions.Label));
            builder.Services.AddTransient<PurchaseOrderSyncRunner>();
            builder.Services.AddTransient<ProductionOrderSyncRunner>();

            using var host = builder.Build();
            DependencyInjection.InitializeEncryption(host.Services);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Log.Warning("Cancellation requested — finishing current SAP call…");
            };

            var document = ResolveDocument(args);
            var exitCode = 0;

            if (document is "purchase-orders" or "both")
            {
                using var scope = host.Services.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<PurchaseOrderSyncRunner>();
                exitCode = await runner.RunAsync(cts.Token);
            }

            if (document is "production-orders" or "both")
            {
                // A fresh scope so each runner gets its own DbContext / change tracker.
                using var scope = host.Services.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<ProductionOrderSyncRunner>();
                var productionExit = await runner.RunAsync(cts.Token);
                exitCode = exitCode != 0 ? exitCode : productionExit;
            }

            return exitCode;
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Sync cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Sync terminated unexpectedly.");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Which document family to sync. Defaults to purchase orders so existing schedules keep
    /// their current behaviour.
    /// </summary>
    private static string ResolveDocument(string[] args)
    {
        var value = ArgValue(args, "--document")?.Trim().ToLowerInvariant();
        return value switch
        {
            "production" or "production-orders" or "po-production" => "production-orders",
            "both" or "all" => "both",
            _ => "purchase-orders",
        };
    }

    /// <summary>
    /// Optional CLI overrides (appsettings remains the primary config source):
    ///   --document purchase-orders|production-orders|both
    ///   --mode new|full|one          (production orders also accept: open)
    ///   --company PBBPL_UAT
    ///   --after-doc-entry 1200       (production orders: --after-absolute-entry)
    ///   --doc-entry 1500             (production orders: --absolute-entry)
    /// </summary>
    private static void ApplyCliOverrides(IConfiguration configuration, string[] args)
    {
        var mode = ArgValue(args, "--mode");
        var company = ArgValue(args, "--company");
        var after = ArgValue(args, "--after-doc-entry");
        var docEntry = ArgValue(args, "--doc-entry");
        var afterAbsolute = ArgValue(args, "--after-absolute-entry");
        var absoluteEntry = ArgValue(args, "--absolute-entry");

        if (mode is not null)
        {
            configuration[$"{PurchaseOrderSyncOptions.Label}:Mode"] = mode;
            configuration[$"{ProductionOrderSyncOptions.Label}:Mode"] = mode;
        }

        if (company is not null)
        {
            configuration[$"{PurchaseOrderSyncOptions.Label}:CompanyDb"] = company;
            configuration[$"{ProductionOrderSyncOptions.Label}:CompanyDb"] = company;
        }

        if (after is not null)
            configuration[$"{PurchaseOrderSyncOptions.Label}:AfterDocEntry"] = after;
        if (docEntry is not null)
            configuration[$"{PurchaseOrderSyncOptions.Label}:DocEntry"] = docEntry;
        if (afterAbsolute is not null)
            configuration[$"{ProductionOrderSyncOptions.Label}:AfterAbsoluteEntry"] = afterAbsolute;
        if (absoluteEntry is not null)
            configuration[$"{ProductionOrderSyncOptions.Label}:AbsoluteEntry"] = absoluteEntry;
    }

    private static string? ArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }
}
