using System;
using System.IdentityModel.Tokens.Jwt;

namespace RotatingJwt
{
    /// <summary>
    /// Represents configuration options for rotating JWT authentication.
    /// </summary>
    public class RotatingJwtOptions : JwtSecurityToken
    {
        /// <summary>
        /// Gets or sets the AES key size used for encryption.
        /// Default value is 256 bits.
        /// </summary>
        public int AesKeySize { get; set; } = 256;

        /// <summary>
        /// Gets or sets the secret key used for signing and encryption.
        /// </summary>
        public string SecretKey { get; set; }

        /// <summary>
        /// Gets or sets the lifetime duration of the JWT access token.
        /// </summary>
        public TimeSpan TokenLifeTime { get; set; }

        /// <summary>
        /// Gets or sets the lifetime duration of the refresh token.
        /// </summary>
        public TimeSpan RefreshTokenLifeTime { get; set; }

        /// <summary>
        /// Gets or sets the audience for which the JWT token is intended.
        /// </summary>
        public string Audience { get; set; }

#if USEREDIS
        /// <summary>
        ///  Gets or sets the connection string for the Redis cache.
        /// </summary>
        public string? RedisConnectionString { get; internal set; }
#endif
        /// <summary>
        /// Gets or sets the issuer for which the JWT token was issued.
        /// </summary>
        public new string Issuer { get; set; }
    }
}