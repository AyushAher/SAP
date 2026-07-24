using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Serilog;
#if USEREDIS
using StackExchange.Redis;
#endif

namespace RotatingJwt
{
    /// <summary>
    /// Provides extension methods for configuring JWT authentication services.
    /// </summary>
    public static class ServiceExtension
    {
        /// <summary>
        /// Gets or sets the JWT options for configuring token properties.
        /// </summary>
        internal static JwtConfiguration JwtOptions { get; set; } = new();

        /// <summary>
        /// Configures rotating JWT authentication services.
        /// </summary>
        public static void AddRotatingJwt(this IServiceCollection services,
            IConfiguration configuration,
#if USEREDIS
            string redisConnectionString,
#endif
            Func<JwtConfiguration, JwtConfiguration>? options = null)
        {
            JwtOptions = options?.Invoke(BuildFromConfiguration(configuration)) ?? BuildFromConfiguration(configuration);

#if USEREDIS
            JwtOptions.Config.RedisConnectionString = redisConnectionString;
            if (string.IsNullOrEmpty(JwtOptions.Config.RedisConnectionString))
                throw new NullReferenceException("Redis connection string is null");
#endif

            if (string.IsNullOrEmpty(JwtOptions.Config.SecretKey))
            {
                using var aes = Aes.Create();
                aes.KeySize = 256;
                aes.GenerateKey();
                JwtOptions.Config.SecretKey = Convert.ToBase64String(aes.Key);
                JwtOptions.Config.AesKeySize = 256;
                Log.Warning("Jwt SecretKey was not configured; a transient key was generated for this process.");
            }

#if USEREDIS
            var redis = ConnectionMultiplexer.Connect(JwtOptions.Config.RedisConnectionString!);
            services.AddSingleton<IConnectionMultiplexer>(redis);
            services.AddScoped<ICacheConfiguration, RedisCacheConfiguration>();
#else
            services.AddMemoryCache();
            services.AddScoped<ICacheConfiguration, MemoryCacheConfiguration>();
#endif

            services.AddHttpContextAccessor();

            services.AddAuthentication("CustomScheme")
                .AddScheme<AuthenticationSchemeOptions, CustomAuthenticationHandler>("CustomScheme", null);

            services.AddAuthorizationBuilder()
                .SetDefaultPolicy(new AuthorizationPolicyBuilder("CustomScheme")
                    .RequireAuthenticatedUser()
                    .Build())
                .AddPolicy("AdminOrSuperAdmin", policy =>
                    policy.AddAuthenticationSchemes("CustomScheme")
                        .RequireAuthenticatedUser()
                        .RequireRole("Admin", "SuperAdmin"));

            services.AddScoped<IJwtTokenService, JwtTokenService>();

            Log.Information("Rotating JWT configured (access {TokenLifeTime}, refresh {RefreshTokenLifeTime})",
                JwtOptions.Config.TokenLifeTime,
                JwtOptions.Config.RefreshTokenLifeTime);
        }

        private static JwtConfiguration BuildFromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection("Jwt");
            var secretKey = section["SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
                secretKey = DeriveSecretKeyFromLegacyKey(section["Key"]);

            var tokenLifeTime = ParseTimeSpan(section["TokenLifeTime"], defaultValue: TimeSpan.FromHours(8));
            if (tokenLifeTime == TimeSpan.FromHours(8)
                && int.TryParse(section["TokenLifeTimeMinutes"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tokenMinutes))
            {
                tokenLifeTime = TimeSpan.FromMinutes(tokenMinutes);
            }

            var refreshLifeTime = ParseTimeSpan(section["RefreshTokenLifeTime"], defaultValue: TimeSpan.FromDays(2));
            if (refreshLifeTime == TimeSpan.FromDays(2)
                && int.TryParse(section["RefreshTokenLifeTimeDays"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var refreshDays))
            {
                refreshLifeTime = TimeSpan.FromDays(refreshDays);
            }

            return new JwtConfiguration
            {
                Config = new RotatingJwtOptions
                {
#if USEREDIS
                    RedisConnectionString = section["RedisConnectionString"],
#endif
                    SecretKey = secretKey ?? string.Empty,
                    Issuer = section["Issuer"] ?? "SapApi",
                    Audience = section["Audience"] ?? "SapApi",
                    TokenLifeTime = tokenLifeTime,
                    RefreshTokenLifeTime = refreshLifeTime,
                    AesKeySize = 256,
                },
                TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                },
            };
        }

        private static TimeSpan ParseTimeSpan(string? value, TimeSpan defaultValue) =>
            !string.IsNullOrWhiteSpace(value) && TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : defaultValue;

        private static string? DeriveSecretKeyFromLegacyKey(string? legacyKey)
        {
            if (string.IsNullOrWhiteSpace(legacyKey))
                return null;

            try
            {
                var bytes = Convert.FromBase64String(legacyKey);
                if (bytes.Length == 32)
                    return legacyKey;
            }
            catch (FormatException)
            {
                // Legacy symmetric string key — derive a stable 256-bit AES key.
            }

            return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(legacyKey)));
        }
    }
}
