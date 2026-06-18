using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SapApi.Domain.Interfaces;

namespace SapApi.Infrastructure.Security;

public class EncryptedStringConverter : ValueConverter<string?, string?>
{
    private static IAesEncryptionService? _encryptionService;

    public static void Initialize(IAesEncryptionService encryptionService) =>
        _encryptionService = encryptionService;

    public static EncryptedStringConverter Instance { get; } = new();

    private EncryptedStringConverter() : base(
        v => _encryptionService == null ? v : _encryptionService.Encrypt(v ?? string.Empty),
        v => _encryptionService == null ? v : _encryptionService.Decrypt(v ?? string.Empty))
    {
    }
}
