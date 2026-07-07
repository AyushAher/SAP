using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RotatingJwt;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Caching;
using SapApi.Infrastructure.Identity;
using SapApi.Infrastructure.Sap;
using SapApi.Infrastructure.Security;
using SapApi.Infrastructure.Services;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Configuration;

namespace SapApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var appConfig = configuration.GetSection(ApplicationConfiguration.Label);
        services.Configure<ApplicationConfiguration>(appConfig);
        services.Configure<SapCredentials>(configuration.GetSection(SapCredentials.Label));
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.Label));

        Constants.SapServiceLayerUrl = appConfig.GetSection("SapServiceLayerUrl").Value
            ?? throw new ArgumentNullException("SapServiceLayerUrl");
        Constants.AuthServiceUrl = appConfig.GetSection("AuthServiceUrl").Value ?? string.Empty;

        var connectionString = configuration.GetConnectionString("DbConnection")
            ?? throw new InvalidOperationException("DbConnection is required");

        var useInMemory = configuration.GetValue<bool>("Testing:UseInMemoryDatabase");

        if (useInMemory)
        {
            var dbName = configuration["Testing:DatabaseName"] ?? Guid.NewGuid().ToString();
            services.AddDbContextFactory<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        }
        else
        {
            Action<DbContextOptionsBuilder> configure = options =>
            {
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.EnableRetryOnFailure(3);
                    npgsql.CommandTimeout(30);
                });
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            };

            services.AddDbContextPool<AppDbContext>(configure, poolSize: 64);
            services.AddPooledDbContextFactory<AppDbContext>(configure, poolSize: 64);
        }

        services.AddMemoryCache();
        AddDistributedCache(services, configuration);
        services.AddSingleton<ISapSessionStore, DistributedCacheSapSessionStore>();

        var redisConnection = RedisConnectionStringNormalizer.Normalize(
            configuration.GetConnectionString("RedisConnection"));
        var useInMemoryDatabase = configuration.GetValue<bool>("Testing:UseInMemoryDatabase");

        services.AddRotatingJwt(configuration);
        if (!useInMemoryDatabase && !string.IsNullOrWhiteSpace(redisConnection))
            services.AddScoped<ICacheConfiguration, DistributedJwtCacheConfiguration>();

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 1;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentCompanyDbAccessor, CurrentCompanyDbAccessor>();
        services.AddScoped<ICurrentBranchAccessor, CurrentBranchAccessor>();
        services.AddSingleton<IAesEncryptionService, AesEncryptionService>();
        services.AddSingleton<IRsaDecryptionService, RsaDecryptionService>();
        services.AddSingleton<IHmacVerificationService, HmacVerificationService>();
        services.AddScoped<ICacheService, HybridCacheService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        services.AddHttpClient<IHttpRequestHandler, HttpRequestHandler>(client =>
        {
            client.Timeout = TimeSpan.FromHours(1);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.None,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            MaxConnectionsPerServer = 20
        });

        if (configuration.GetValue<bool>("Testing:UseNoOpSapLogin"))
            services.AddScoped<ISapLoginService, NoOpSapLoginService>();
        else
            services.AddScoped<ISapLoginService, SapLoginService>();

        services.AddScoped<ApprovalService>();
        services.AddScoped<ApprovalExecutionService>();
        services.AddScoped<ApprovalPolicyService>();
        services.AddScoped<ApprovalRequestViewService>();
        services.AddScoped<StageWisePaymentService>();
        services.AddScoped<StageWisePaymentPageService>();
        services.AddScoped<StageWisePaymentBatchService>();
        services.AddScoped<StageWisePaymentPdfBuilder>();
        services.AddScoped<IssueForProductionService>();
        services.AddScoped<ReceiptFromProductionService>();
        services.AddScoped<ProductionOrderSelectionService>();
        services.AddScoped<SapMasterDataService>();
        services.AddScoped<BusinessPartnerService>();
        services.AddScoped<SapPurchaseDownPaymentService>();
        services.AddScoped<SapVendorPaymentService>();
        services.AddScoped<InventoryItemsTransferService>();
        services.AddScoped<SapPurchaseOrderService>();
        services.AddScoped<SapTaxCodesService>();
        services.AddScoped<SapProductionOrdersService>();
        services.AddScoped<SapInventoryGenExitsService>();
        services.AddScoped<SapSalesOrdersService>();
        services.AddScoped<IPdfService, PdfService>();

        return services;
    }

    public static void InitializeEncryption(IServiceProvider services)
    {
        var aes = services.GetRequiredService<IAesEncryptionService>();
        EncryptedStringConverter.Initialize(aes);
    }

    private static void AddDistributedCache(IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = RedisConnectionStringNormalizer.Normalize(
            configuration.GetConnectionString("RedisConnection"));
        var useInMemoryDatabase = configuration.GetValue<bool>("Testing:UseInMemoryDatabase");

        if (!useInMemoryDatabase && !string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "SapApi:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }
    }
}
