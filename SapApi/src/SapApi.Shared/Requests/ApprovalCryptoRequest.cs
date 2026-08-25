using System.Text.Json;

namespace SapApi.Shared.Requests;

/// <summary>
/// Body for AES encrypt/decrypt of the value stored in <c>ApprovalRequests.RequestBody</c>
/// (and SupportingData). Same key as <c>EncryptedStringConverter</c>.
/// </summary>
public class ApprovalCryptoRequest
{
    /// <summary>
    /// Plaintext JSON string to encrypt, or the Base64 AES ciphertext copied from the database.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Optional JSON object to encrypt instead of <see cref="Text"/>.
    /// </summary>
    public JsonElement? Json { get; set; }
}
