namespace SapApi.Shared.Configuration;

public class SecurityOptions
{
    public const string Label = "Security";

    public string HmacSecret { get; set; } = string.Empty;
    public string AesKeyBase64 { get; set; } = string.Empty;
    public string RsaPrivateKeyPath { get; set; } = "Keys/private.pem";
    public string RsaPublicKeyPath { get; set; } = "Keys/public.pem";
    public bool RequireHmac { get; set; } = true;
    public bool RequireEncryptedPayload { get; set; } = true;
}
