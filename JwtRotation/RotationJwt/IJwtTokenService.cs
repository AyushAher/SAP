using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace RotatingJwt
{
    /// <summary>
    /// Interface for JWT token service.
    /// </summary>
    public interface IJwtTokenService
    {
        /// <summary>
        /// Generates an access token for the given user ID.
        /// </summary>
        /// <param name="userId">The user ID to encode into the token.</param>
        /// <returns>A <see cref="TokenResponse"/> containing the generated token and keys.</returns>
        Task<TokenResponse> GenerateAccessToken(string userId);

        /// <summary>
        /// Generates an access token with additional claims and refresh token option.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="additionalClaims">Additional claims to include.</param>
        /// <param name="needRefreshToken">Whether a refresh token is needed.</param>
        /// <returns>A <see cref="TokenResponse"/> with the token details.</returns>
        Task<TokenResponse> GenerateAccessToken(string userId, List<Claim> additionalClaims, bool needRefreshToken);

        /// <summary>
        /// Generates a new access token using a refresh token.
        /// </summary>
        /// <param name="refreshToken">The refresh token.</param>
        /// <param name="claims">The cliams.</param>
        /// <returns>A new <see cref="TokenResponse"/>.</returns>
        Task<TokenResponse> GenerateTokenByRefreshToken(string refreshToken, List<Claim>? claims = null);

        /// <summary>
        /// Resolves the user id embedded in a refresh token without issuing a new access token.
        /// </summary>
        Task<string?> ResolveUserIdFromRefreshTokenAsync(string refreshToken);

        /// <summary>
        /// Validates the token from the HTTP request.
        /// </summary>
        /// <returns>A <see cref="TokenValidationResult"/> indicating the validation status.</returns>
        Task<TokenValidationResult> ValidateParameters();

        /// <summary>
        /// Validates a given JWT token.
        /// </summary>
        /// <param name="token">The JWT token.</param>
        /// <returns>A <see cref="TokenValidationResult"/> representing the validation outcome.</returns>
        Task<TokenValidationResult> ValidateParameters(string token);
    }
}
