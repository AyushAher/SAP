using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace RotatingJwt
{
    /// <summary>
    /// Service for generating and validating JWT tokens with fingerprinting.
    /// </summary>
    public class JwtTokenService : IJwtTokenService
    {
        private readonly ICacheConfiguration _cacheConfiguration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="JwtTokenService"/> class.
        /// </summary>
        /// <param name="cacheConfiguration">The memory cache instance.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        public JwtTokenService(ICacheConfiguration cacheConfiguration, IHttpContextAccessor httpContextAccessor)
        {
            _cacheConfiguration = cacheConfiguration;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Generates an access token for the given user ID.
        /// </summary>
        /// <param name="userId">The user ID to encode into the token.</param>
        /// <returns>A <see cref="TokenResponse"/> containing the generated token and keys.</returns>
        public async Task<TokenResponse> GenerateAccessToken(string userId)
        {
            return await GenerateAccessToken(userId, new List<Claim>(), false);
        }

        /// <summary>
        /// Generates an access token.
        /// </summary>
        public async Task<TokenResponse> GenerateAccessToken(string userId, List<Claim> additionalClaims,
            bool needRefreshToken)
        {
            var keyId = Guid.NewGuid().ToString("N"); // or a hash of the public key

            var encryptedUserId = userId.Encrypt(ServiceExtension.JwtOptions.Config.SecretKey);
            using var rsa = new RSACryptoServiceProvider(4096);

            var privateKeyXml = rsa.ToXmlString(true);
            var publicKeyXml = rsa.ToXmlString(false);

            var key = new RsaSecurityKey(rsa)
            {
                KeyId = keyId
            };
            var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

            var fingerprint = GenerateFingerprint();

            if (additionalClaims.All(c => c.Type != "fingerprint"))
            {
                additionalClaims.Add(new Claim("fingerprint", fingerprint));
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Authentication, encryptedUserId)
            };

            claims.AddRange(additionalClaims);
            var token = new JwtSecurityToken(new JwtHeader(credentials),
                new JwtPayload(
                    ServiceExtension.JwtOptions.Config.Issuer,
                    ServiceExtension.JwtOptions.Config.Audience,
                    claims,
                    DateTime.UtcNow,
                    DateTime.UtcNow.Add(ServiceExtension.JwtOptions.Config.TokenLifeTime)));

            var response = new TokenResponse
            {
                PrivateKey = privateKeyXml.Encrypt(ServiceExtension.JwtOptions.Config.SecretKey),
                PublicKey = publicKeyXml.Encrypt(ServiceExtension.JwtOptions.Config.SecretKey),
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                FingerPrint = fingerprint,
                KId = keyId
            };

            if (needRefreshToken)
            {
                response.RefreshToken = await GenerateRefreshToken(userId, keyId);
            }

            await _cacheConfiguration.SetInCacheMemoryAsync(keyId, response,
                (int)ServiceExtension.JwtOptions.Config.TokenLifeTime.TotalMinutes);

            return response;
        }

        /// <summary>
        /// Generates a refresh token for the specified user ID and key ID.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="keyId"></param>
        /// <returns></returns>
        private async Task<string> GenerateRefreshToken(string userId, string keyId)
        {
            var refreshToken = Guid.NewGuid().ToString();
            var combinedToken = $"{refreshToken}@{keyId}@{userId}";
            var encryptedToken = combinedToken.Encrypt(ServiceExtension.JwtOptions.Config.SecretKey);
            var refreshTokenEntry = new RefreshTokenEntry
            {
                EncryptedData = encryptedToken,
                ExpiresAt = DateTime.UtcNow.Add(ServiceExtension.JwtOptions.Config.RefreshTokenLifeTime)
            };
            await _cacheConfiguration.SetInCacheMemoryAsync($"refresh_{refreshToken}",
                refreshTokenEntry, (int)ServiceExtension.JwtOptions.Config.RefreshTokenLifeTime.TotalMinutes);

            return refreshToken;
        }


        /// <summary>
        /// Generates a new access token using a refresh token.
        /// </summary>
        public async Task<TokenResponse> GenerateTokenByRefreshToken(string refreshToken, List<Claim>? claims = null)
        {
            var userId = await ResolveUserIdFromRefreshTokenAsync(refreshToken);
            if (userId is null)
                throw new SecurityTokenException("Invalid refresh token.");

            return await GenerateAccessToken(userId, claims ?? [], true);
        }

        /// <inheritdoc />
        public async Task<string?> ResolveUserIdFromRefreshTokenAsync(string refreshToken)
        {
            var tokenResponse =
                await _cacheConfiguration.GetFromCacheMemoryByIdAsync<RefreshTokenEntry>($"refresh_{refreshToken}");

            if (tokenResponse is null || DateTime.UtcNow > tokenResponse.ExpiresAt)
                return null;

            var decryptedData = tokenResponse.EncryptedData.Decrypt(ServiceExtension.JwtOptions.Config.SecretKey);
            var parts = decryptedData.Split('@');
            return parts.Length == 3 && parts[0] == refreshToken ? parts[2] : null;
        }


        /// <summary>
        /// Validates the token extracted from the HTTP request.
        /// </summary>
        /// <returns>A <see cref="TokenValidationResult"/> representing the validation outcome.</returns>
        public async Task<TokenValidationResult> ValidateParameters()
        {
            var token = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString()
                .Replace("Bearer ", "") ?? "";

            return await ValidateParameters(token);
        }

        /// <summary>
        /// Validates a given JWT token.
        /// </summary>
        /// <param name="token">The JWT token to validate.</param>
        /// <returns>A <see cref="TokenValidationResult"/> representing the validation outcome.</returns>
        public async Task<TokenValidationResult> ValidateParameters(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return new TokenValidationResult { IsValid = false, Error = "Token is missing" };
            }

            using var rsa = new RSACryptoServiceProvider();
            var handler = new JwtSecurityTokenHandler();

            // Read and parse the token
            var jwtToken = handler.ReadJwtToken(token);

            // Extract claims
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Authentication)?.Value;

            if (userId is null)
            {
                return new TokenValidationResult { IsValid = false, Error = "Claims mismatch detected!" };
            }

            var tokenFingerprint = jwtToken.Claims.FirstOrDefault(c => c.Type == "fingerprint")?.Value;

            var cachedTokenObj =
                await _cacheConfiguration.GetFromCacheMemoryByIdAsync<TokenResponse?>(jwtToken.Header.Kid);

            if (cachedTokenObj is null)
            {
                return new TokenValidationResult { IsValid = false, Error = "Invalid or expired token" };
            }

            var cachedTokenData = (dynamic?)cachedTokenObj;
            var cachedFingerprint = cachedTokenObj.FingerPrint;

            // Compare fingerprint from token with request fingerprint
            var requestFingerprint = GenerateFingerprint();
            if (tokenFingerprint != requestFingerprint || tokenFingerprint != cachedFingerprint)
            {
                return new TokenValidationResult
                { IsValid = false, Error = "Fingerprint mismatch! Possible token reuse." };
            }

            if (cachedTokenData is null)
            {
                return new TokenValidationResult { IsValid = false, Error = "Invalid token data" };
            }

            var keyXmlString = cachedTokenObj.PublicKey.Decrypt(ServiceExtension.JwtOptions.Config.SecretKey);
            rsa.FromXmlString(keyXmlString);

            var key = new RsaSecurityKey(rsa)
            {
                KeyId = jwtToken.Header.Kid
            };

            var baseParameters = ServiceExtension.JwtOptions.TokenValidationParameters;

            var parameters = new TokenValidationParameters
            {
                ValidIssuer = baseParameters.ValidIssuer,
                ValidAudience = baseParameters.ValidAudience,
                ValidateIssuer = baseParameters.ValidateIssuer,
                ValidateAudience = baseParameters.ValidateAudience,
                ValidateLifetime = baseParameters.ValidateLifetime,
                RequireExpirationTime = baseParameters.RequireExpirationTime,
                ValidateIssuerSigningKey = baseParameters.ValidateIssuerSigningKey,
                ClockSkew = baseParameters.ClockSkew,
                IssuerSigningKey = key,
                IssuerSigningKeyResolver = (_, _, kid, _) =>
                {
                    var response = _cacheConfiguration.GetFromCacheMemoryByIdAsync<TokenResponse?>(kid).Result;
                    if (response is null) return Enumerable.Empty<SecurityKey>();

                    var rsaKey = new RSACryptoServiceProvider();
                    rsaKey.FromXmlString(response.PublicKey.Decrypt(ServiceExtension.JwtOptions.Config.SecretKey));
                    return [new RsaSecurityKey(rsaKey) { KeyId = kid }];
                }
            };

            var principal = handler.ValidateToken(token, parameters, out _);
            return new TokenValidationResult { IsValid = true, ClaimsPrincipal = principal };
        }


        /// <summary>
        /// Generates a fingerprint based on client request data.
        /// </summary>
        /// <returns>A Base64-encoded SHA256 hash representing the request fingerprint.</returns>
        private string GenerateFingerprint()
        {
            var context = _httpContextAccessor.HttpContext;
            var userAgent = context?.Request.Headers.UserAgent.FirstOrDefault() ?? "UnknownUA";
            var acceptLang = context?.Request.Headers.AcceptLanguage.FirstOrDefault() ?? "UnknownLang";
            var ipAddress = context?.Connection.RemoteIpAddress?.ToString() ?? "UnknownIP";
            var rawData = $"{userAgent}-{acceptLang}-{ipAddress}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToBase64String(hashBytes);
        }

        public static string ConvertEncKeyToPem(string key)
        {
            var xml = key.Decrypt(ServiceExtension.JwtOptions.Config.SecretKey);

            using var rsa = RSA.Create();
            rsa.FromXmlString(xml);

            var publicKey = rsa.ExportSubjectPublicKeyInfo(); // X.509 SubjectPublicKeyInfo
            var base64 = Convert.ToBase64String(publicKey);
            var sb = new StringBuilder();
            sb.AppendLine("-----BEGIN PUBLIC KEY-----");

            for (int i = 0; i < base64.Length; i += 64)
            {
                sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
            }

            sb.AppendLine("-----END PUBLIC KEY-----");
            return sb.ToString();
        }


        public static RsaSecurityKey ConvertToRsaSecurityKey(SecurityKey securityKey, string kid)
        {
            switch (securityKey)
            {
                case RsaSecurityKey rsaSecurityKey:
                    // Already an RSA key
                    return rsaSecurityKey;
                case X509SecurityKey x509SecurityKey:
                    {
                        // Extract RSA public key from certificate
                        var rsa = x509SecurityKey.Certificate.GetRSAPublicKey();
                        if (rsa == null)
                            throw new InvalidOperationException("The certificate does not contain an RSA public key.");

                        var rsaKey = new RsaSecurityKey(rsa)
                        {
                            KeyId = x509SecurityKey.KeyId // Preserve KeyId
                        };
                        return rsaKey;
                    }
                default:
                    throw new NotSupportedException(
                        $"Cannot convert security key of type '{securityKey.GetType().Name}' to RsaSecurityKey.");
            }
        }

    }
}