using System.Security.Claims;

namespace RotatingJwt
{
    /// <summary>
    /// Represents the result of a JWT token validation process.
    /// </summary>
    public class TokenValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the token is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the error message if the token validation fails.
        /// Null if validation is successful.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Gets or sets the claims principal extracted from the validated token.
        /// Null if validation fails.
        /// </summary>
        public ClaimsPrincipal ClaimsPrincipal { get; set; }
    }
}