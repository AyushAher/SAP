using Hangfire;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using SapForm.Services;
using SapForm.Services.Helpers;
using SapForm.Services.Login;
using SapForm.Services.Sap_Sevices;
using Shared;
using Shared.Configuration;
using Shared.Entities;
using StackExchange.Redis;
using System.Net;

namespace SapForm
{
    public static class ServiceExtensions
    {
        public static void AddServices(this IServiceCollection services, IConfiguration configuration)
        {
            // -----------------------------
            // Configuration Binding
            // -----------------------------
            IConfigurationSection appConfig = configuration.GetSection(ApplicationConfiguration.Label);
            services.Configure<ApplicationConfiguration>(appConfig);
            services.Configure<SapCredentials>(configuration.GetSection(SapCredentials.Label));

            Constants.SapServiceLayerUrl =
                appConfig.GetSection("SapServiceLayerUrl").Value
                ?? throw new ArgumentNullException("SapServiceLayerUrl");

            Constants.AuthServiceUrl =
                appConfig.GetSection("AuthServiceUrl").Value;

            // -----------------------------
            // Database
            // -----------------------------
            services.AddDbContext<Context>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DbConnection")));

            // -----------------------------
            // Identity (ONLY ONCE)
            // -----------------------------
            services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<Context>()
            .AddDefaultTokenProviders();

            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/Login";

                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
            });

            services.AddAuthentication();   // Required
            services.AddAuthorization();
            services.AddCascadingAuthenticationState();

            // -----------------------------
            // Http & Context
            // -----------------------------
            services.AddHttpContextAccessor();

            services.AddScoped(sp =>
            {
                NavigationManager nav = sp.GetRequiredService<NavigationManager>();
                return new HttpClient
                {
                    BaseAddress = new Uri(nav.BaseUri)
                };
            });
            // -----------------------------
            // Application Services
            // -----------------------------
            services.AddScoped<IHttpRequestHandler, HttpRequestHandler>();
            services.AddHttpClient<IHttpRequestHandler, HttpRequestHandler>(options =>
            {
                options.Timeout = TimeSpan.FromHours(1);
            })
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new HttpClientHandler
                    {
                        UseProxy = false,
                        AutomaticDecompression = DecompressionMethods.None,
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    });

            services.AddScoped<ApprovalPolicyService>();
            services.AddScoped<ApprovalService>();
            services.AddScoped<LoginService>();
            services.AddScoped<UserSession>();

            services.AddScoped<SapPurchaseDownPaymentService>();
            services.AddScoped<SapVendorPaymentService>();
            services.AddScoped<InventoryItemsTransferService>();
            services.AddScoped<SapPurchaseOrderService>();
            services.AddScoped<SapTaxCodesService>();
            services.AddScoped<BusinessPartnerService>();
            services.AddScoped<SapProductionOrdersService>();
            services.AddScoped<SapInventoryGenExitsService>();
            services.AddScoped<SapSalesOrdersService>();
            services.AddScoped<PdfService>();
            services.AddSingleton<LoaderService>();
            services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, CustomClaimsPrincipalFactory>();
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("RedisConnection");
            });

            services.AddHangfire(x =>
                x.UseSqlServerStorage(configuration.GetConnectionString("DbConnection"))
            );
            services.AddHangfireServer();
            services.AddScoped<RedisCacheService>();
            services.AddScoped<CacheRefreshJob>();
            services.AddScoped<StageWisePaymentService>();
        }
    }


    public class AmountInWords
    {
        public static string ConvertToWords(double amount)
        {
            long rupees = (long)Math.Floor(amount);
            int paise = (int)((amount - rupees) * 100);

            string result = $"{NumberToWords(rupees)} Rupees";

            if (paise > 0)
                result += $" and {NumberToWords(paise)} Paise";

            return result + " Only";
        }

        private static string NumberToWords(long number)
        {
            if (number == 0)
                return "Zero";

            string[] units = {
                "", "One", "Two", "Three", "Four", "Five", "Six",
                "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve",
                "Thirteen", "Fourteen", "Fifteen", "Sixteen",
                "Seventeen", "Eighteen", "Nineteen"
            };

            string[] tens = {
                "", "", "Twenty", "Thirty", "Forty", "Fifty",
                "Sixty", "Seventy", "Eighty", "Ninety"
            };

            if (number < 20)
                return units[number];

            if (number < 100)
                return tens[number / 10] + (number % 10 > 0 ? " " + units[number % 10] : "");

            if (number < 1000)
                return units[number / 100] + " Hundred" +
                       (number % 100 > 0 ? " " + NumberToWords(number % 100) : "");

            if (number < 100000)
                return NumberToWords(number / 1000) + " Thousand" +
                       (number % 1000 > 0 ? " " + NumberToWords(number % 1000) : "");

            if (number < 10000000)
                return NumberToWords(number / 100000) + " Lakh" +
                       (number % 100000 > 0 ? " " + NumberToWords(number % 100000) : "");

            return NumberToWords(number / 10000000) + " Crore" +
                   (number % 10000000 > 0 ? " " + NumberToWords(number % 10000000) : "");
        }
    }
}