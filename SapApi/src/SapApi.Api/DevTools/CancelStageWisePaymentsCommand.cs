using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure;
using SapApi.Infrastructure.Identity;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services;
using SapApi.Shared;
using SapApi.Shared.Configuration;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;

namespace SapApi.Api.DevTools;

public static class CancelStageWisePaymentsCommand
{
    public static async Task<int> TryRunAsync(string[] args, IConfiguration configuration)
    {
        if (args.Length < 2 || !string.Equals(args[0], "cancel-payments", StringComparison.OrdinalIgnoreCase))
            return -1;

        var paymentIds = args.Skip(1)
            .Select(id => int.TryParse(id, out var parsed) ? parsed : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray();

        if (paymentIds.Length == 0)
        {
            Console.Error.WriteLine("Usage: dotnet run -- cancel-payments <paymentId> [paymentId...]");
            return 1;
        }

        var sapCredentials = configuration.GetSection(SapCredentials.Label).Get<SapCredentials>() ?? new SapCredentials();
        var companyDbName = Environment.GetEnvironmentVariable("SAP_COMPANY_DB")
            ?? sapCredentials.Accounts.FirstOrDefault()?.CompanyDb
            ?? "PBBPL_UAT";
        var companyDb = Enum.TryParse<SapCompanyDatabase>(companyDbName, out var parsedCompanyDb)
            ? parsedCompanyDb
            : SapCompanyDatabase.PBBPL_UAT;
        var account = sapCredentials.Accounts.FirstOrDefault(a =>
                          string.Equals(a.CompanyDb, companyDb.ToString(), StringComparison.OrdinalIgnoreCase))
                      ?? sapCredentials.Accounts.FirstOrDefault()
                      ?? new SapCompanyCredential();

        var httpContextAccessor = new DevHttpContextAccessor();
        httpContextAccessor.HttpContext = CreateHttpContext(4, companyDb);

        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.Sources.Clear();
                config.AddConfiguration(configuration);
            })
            .ConfigureServices(services =>
            {
                services.AddInfrastructure(configuration);
                services.AddSingleton<IHttpContextAccessor>(httpContextAccessor);
            })
            .Build();

        using var scope = host.Services.CreateScope();
        DependencyInjection.InitializeEncryption(scope.ServiceProvider);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var sapLogin = scope.ServiceProvider.GetRequiredService<ISapLoginService>();
        try
        {
            await sapLogin.SapLoginAsync();
        }
        catch (ApiErrorException)
        {
            var sapUser = Environment.GetEnvironmentVariable("SAP_DEV_USERNAME") ?? account.Username ?? "manager";
            var sapPassword = Environment.GetEnvironmentVariable("SAP_DEV_PASSWORD")
                ?? account.Password
                ?? Environment.GetEnvironmentVariable("SAP_PASSWORD");
            if (string.IsNullOrWhiteSpace(sapPassword))
            {
                Console.Error.WriteLine("No active SAP session found. Log in to the app or set SAP_DEV_PASSWORD / SapCredentials Accounts password.");
                return 1;
            }

            await sapLogin.LoginWithUserCredentialsAsync(4, sapUser, sapPassword, companyDb);
        }

        var paymentService = scope.ServiceProvider.GetRequiredService<StageWisePaymentService>();
        var exitCode = 0;

        foreach (var paymentId in paymentIds)
        {
            var record = await db.StageWisePayments
                .FirstOrDefaultAsync(x => x.Id == paymentId && x.CompanyDb == companyDb.ToString());
            if (record is null)
            {
                Console.Error.WriteLine($"Payment {paymentId} not found.");
                exitCode = 1;
                continue;
            }

            if (record.Status == StageWisePaymentStatus.Cancelled)
            {
                Console.WriteLine($"Payment {paymentId} is already cancelled.");
                continue;
            }

            var (success, operations) = await paymentService.CancelOutgoingPayment(record);
            Console.WriteLine($"Payment {paymentId}: {(success ? "cancelled" : "failed")}");
            foreach (var (_, message) in operations)
                Console.WriteLine($"  {message}");

            if (!success)
                exitCode = 1;
        }

        return exitCode;
    }

    static DefaultHttpContext CreateHttpContext(int userId, SapCompanyDatabase companyDb)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(SapClaimTypes.CompanyDb, companyDb.ToString()),
        ],
        authenticationType: "DevCli");

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };
    }

    sealed class DevHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
