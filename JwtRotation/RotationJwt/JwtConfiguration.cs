using Microsoft.IdentityModel.Tokens;

namespace RotatingJwt
{
    /// <summary>
    /// Represents the configuration settings for JWT authentication.
    /// </summary>
    public class JwtConfiguration
    {
        /// <summary>
        /// Gets or sets the rotating JWT options, including token lifetime and security settings.
        /// </summary>
        public RotatingJwtOptions Config { get; set; } = new();

        /// <summary>
        /// Gets or sets the token validation parameters used for verifying JWT tokens.
        /// </summary>
        public TokenValidationParameters TokenValidationParameters { get; set; } = new();
    }
}
