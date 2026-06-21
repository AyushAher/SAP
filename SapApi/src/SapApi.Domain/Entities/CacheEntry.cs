namespace SapApi.Domain.Entities;

public class CacheEntry
{
    public string Key { get; set; } = string.Empty;
    public string CompanyDb { get; set; } = string.Empty;
    public byte[] CompressedValue { get; set; } = [];
    public DateTime ExpiresAtUtc { get; set; }
}
