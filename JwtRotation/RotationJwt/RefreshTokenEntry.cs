using System;

namespace RotatingJwt
{
    public class RefreshTokenEntry
    {
        public string EncryptedData { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
