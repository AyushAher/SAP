using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using SapApi.Modals.Configuration;
using SapApi.Modals;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Reflection;
using SapApi.Services;
using SapApi.Services.Helpers;
using SapApi.Services.Login;
using SapApi.Modals.Entities;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Extensions.Primitives;

namespace SapApi
{
    public static class ServiceExtension
    {
        public static void AddServices(this IServiceCollection service, IConfiguration configuration)
        {
            IConfigurationSection appConfig = configuration.GetSection(ApplicationConfiguration.Label);
            service.Configure<ApplicationConfiguration>(appConfig);
            service.Configure<SapCredentials>(configuration.GetSection(SapCredentials.Label));

            Constants.SapServiceLayerUrl = appConfig.GetSection("SapServiceLayerUrl").Value ??
                                           throw new ArgumentNullException("SapServiceLayerUrl");

            Constants.AuthServiceUrl = appConfig.GetSection("AuthServiceUrl").Value ??
                                           throw new ArgumentNullException("AuthServiceUrl");

            service.AddDbContext<Context>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DbConnection")));



            service.AddHttpContextAccessor();
            service.AddScoped<IHttpRequestHandler, HttpRequestHandler>();
            service.AddScoped<LoginService>();
            service.AddScoped<SapPurchaseDownPaymentService>();
            service.AddScoped<SapVendorPaymentService>();
            service.AddScoped<InventoryItemsTransferService>();
            service.AddScoped<SapPurchaseOrderService>();
            service.AddScoped<SapTaxCodesService>();
            service.AddScoped<AccountService>();
            service.AddScoped<BusinessPartnerService>();
            service.AddScoped<SapProductionOrdersService>();
            service.AddScoped<SapInventoryGenExitsService>();
            service.AddHttpClient();
            service.AddScoped<UserSession>();
        }


        public static void AddAuthenticationConfigServices(this IServiceCollection service)
        {

            //service.AddAuthentication(options =>
            //{
            //    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            //    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            //    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            //})
            //    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            //    {
            //        options.LoginPath = "/Account/Login";
            //        options.AccessDeniedPath = "/Account/Login";

            //        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            //        options.SlidingExpiration = true;

            //        options.Cookie.Name = ".SapApi.Auth";
            //        options.Cookie.HttpOnly = true;

            //        options.Cookie.SameSite = SameSiteMode.Lax;
            //        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

            //        options.Cookie.Path = "/";
            //    });

            service.AddAuthorization();
            service.AddCascadingAuthenticationState();

        }

        /// <summary>
        /// Adds JWT authentication services to the service collection.
        /// </summary>
        /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add the services to.</param>
        /// <param name="configuration">The <see cref="IConfiguration"/> instance.</param>
        /// <returns>The modified <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection serviceCollection,
            IConfiguration configuration)
        {
            var appConfig = "";

            serviceCollection
                .AddAuthentication(authentication =>
                {
                    authentication.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    authentication.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(bearer =>
                {
                    bearer.RequireHttpsMetadata = false;
                    bearer.SaveToken = true;

                    bearer.TokenValidationParameters = new()
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(appConfig)),
                        ValidateIssuerSigningKey = true,
                        RoleClaimType = ClaimTypes.Role,
                    };
                    bearer.Events = new()
                    {
                        OnMessageReceived = context =>
                        {
                            // Get the access token from the query string
                            StringValues accessToken = context.Request.Query["access_token"];

                            // Check if the request path starts with "/hub" to limit token extraction to SignalR hubs
                            PathString path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/NotificationHub"))
                            {
                                context.Token = accessToken;
                            }

                            return Task.CompletedTask;
                        },
                        OnAuthenticationFailed = c =>
                        {
                            if (c.Exception is SecurityTokenExpiredException)
                            {
                                c.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                                c.Response.ContentType = "application/json";
                                var result = JsonConvert.SerializeObject(
                                    ApiResponseModal<object>.Fatal(
                                        new ApiErrorException(BaseErrorCodes.InvalidJwtToken)));
                                return c.Response.WriteAsync(result);
                            }
#if DEBUG
                            c.NoResult();
                            c.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            c.Response.ContentType = "text/plain";
                            return c.Response.WriteAsync(c.Exception.ToString());
#else
                            c.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            c.Response.ContentType = "application/json";
                            var res = JsonConvert.SerializeObject(
                                    ApiResponseModal<object>.Fatal(new ApiErrorException(BaseErrorCodes.UnknownSystemException)));
                            return c.Response.WriteAsync(res);
#endif
                        },
                        OnChallenge = context =>
                        {
                            context.Response.StatusCode = 401;
                            context.Response.ContentType = "application/json";
                            var result = JsonConvert.SerializeObject(
                                ApiResponseModal<object>.Fatal(
                                    new ApiErrorException(BaseErrorCodes.InvalidJwtToken)));
                            return context.Response.WriteAsync(result);
                        },
                        OnForbidden = context =>
                        {
                            context.Response.StatusCode = 403;
                            context.Response.ContentType = "application/json";
                            var result = JsonConvert.SerializeObject(
                                ApiResponseModal<object>.Fatal(
                                    new ApiErrorException(BaseErrorCodes.UnAuthorized)));
                            return context.Response.WriteAsync(result);
                        }
                    };
                });

            serviceCollection.AddHttpContextAccessor();
            return serviceCollection;
        }

        /// <summary>
        /// Gets the table name attribute from the specified entity class.
        /// </summary>
        /// <typeparam name="T">The entity class.</typeparam>
        /// <param name="_">An instance of the entity class.</param>
        /// <returns>The table name string in PostgreSQL-supported format.</returns>
        /// <exception cref="ApiErrorException">Thrown if the table attribute is not configured for the requested entity.</exception>

        public static string GetTable<T>(this T _) where T : class
        {
            TableAttribute tableAttribute = typeof(T).GetCustomAttribute<TableAttribute>()
                                 ?? throw new ApiErrorException(BaseErrorCodes.NullValue,
                                     "Table attribute not configured for the requested entity.");
            return $@"""{tableAttribute.Schema ?? "public"}"".""{tableAttribute.Name}""";
        }


    }
}
