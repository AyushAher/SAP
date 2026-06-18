namespace RotatingJwt
{
    /// <summary>
    /// Represents the response containing the generated JWT token and associated keys.
    /// </summary>
    public class TokenResponse
    {
        /// <summary>
        /// Gets or sets the JWT access token.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Gets or sets the encrypted private key used for signing the token.
        /// </summary>
        public string PrivateKey { get; set; }

        /// <summary>
        /// Gets or sets the encrypted public key used for token validation.
        /// </summary>
        public string PublicKey { get; set; }

        /// <summary>
        /// Gets or sets the JWT refresh token.
        /// </summary>
        public string RefreshToken {  get; set; }

        /// <summary>
        /// Gets or sets the FingerPrint.
        /// </summary>
        public string FingerPrint { get; set; }

        public string KId { get; set; }
    }
}