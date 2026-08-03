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
            builder.Services.AddTransient<PurchaseOrderSyncRunner>();

            using var host = builder.Build();
            DependencyInjection.InitializeEncryption(host.Services);

            using var scope = host.Services.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<PurchaseOrderSyncRunner>();

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Log.Warning("Cancellation requested — finishing current SAP call…");
            };

            return await runner.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Purchase order sync cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Purchase order sync terminated unexpectedly.");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Optional CLI overrides (appsettings remains the primary config source):
    ///   --mode new|full|one
    ///   --company PBBPL_UAT
    ///   --after-doc-entry 1200
    ///   --doc-entry 1500
    /// </summary>
    private static void ApplyCliOverrides(IConfiguration configuration, string[] args)
    {
        var mode = ArgValue(args, "--mode");
        var company = ArgValue(args, "--company");
        var after = ArgValue(args, "--after-doc-entry");
        var docEntry = ArgValue(args, "--doc-entry");

        if (mode is not null)
            configuration[$"{PurchaseOrderSyncOptions.Label}:Mode"] = mode;
        if (company is not null)
            configuration[$"{PurchaseOrderSyncOptions.Label}:CompanyDb"] = company;
        if (after is not null)
            configuration[$"{PurchaseOrderSyncOptions.Label}:AfterDocEntry"] = after;
        if (docEntry is not null)
            configuration[$"{PurchaseOrderSyncOptions.Label}:DocEntry"] = docEntry;
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
