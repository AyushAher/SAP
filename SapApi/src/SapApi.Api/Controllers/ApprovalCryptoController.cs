using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Shared;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;

namespace SapApi.Api.Controllers;

/// <summary>
/// AES helper for approval <c>RequestBody</c> / <c>SupportingData</c> at rest.
/// Intended for Swagger: paste ciphertext from Postgres, or plaintext JSON.
/// </summary>
[ApiController]
[Route("api/approvals/crypto")]
[AllowAnonymous]
[Tags("Approvals")]
public class ApprovalCryptoController(
    IAesEncryptionService aes,
    AppDbContext db,
    ICurrentCompanyDbAccessor companyDbAccessor) : ControllerBase
{
    /// <summary>Encrypts plaintext with the same AES used when saving an approval request body.</summary>
    [HttpPost("encrypt")]
    public IActionResult Encrypt([FromBody] ApprovalCryptoRequest request)
    {
        var plain = ResolvePlaintext(request);
        if (plain is null)
            return BadRequest(ApiResponse<object>.Fail(
                BaseErrorCodes.ValidationFailed,
                "Provide text or json to encrypt."));

        return Ok(ApiResponse<object>.Ok(new { cipherText = aes.Encrypt(plain) }));
    }

    /// <summary>
    /// Decrypts AES ciphertext copied from <c>ApprovalRequests.RequestBody</c> (or SupportingData) in the database.
    /// </summary>
    [HttpPost("decrypt")]
    public IActionResult Decrypt([FromBody] ApprovalCryptoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(ApiResponse<object>.Fail(
                BaseErrorCodes.ValidationFailed,
                "Provide the AES ciphertext from ApprovalRequests.RequestBody."));

        try
        {
            var plainText = aes.Decrypt(request.Text.Trim());
            return Ok(ApiResponse<object>.Ok(new
            {
                plainText,
                json = TryParseJson(plainText),
            }));
        }
        catch (FormatException)
        {
            return BadRequest(DecryptFailed());
        }
        catch (CryptographicException)
        {
            return BadRequest(DecryptFailed());
        }
        catch (ArgumentException)
        {
            return BadRequest(DecryptFailed());
        }
    }

    /// <summary>
    /// Returns the already-decrypted RequestBody and SupportingData for an approval in the current company.
    /// </summary>
    [HttpGet("{requestId:int}")]
    public async Task<IActionResult> GetStoredBody(int requestId, CancellationToken cancellationToken)
    {
        var companyDb = companyDbAccessor.GetCompanyDb()?.ToString();
        var query = db.ApprovalRequests.AsNoTracking().Where(x => x.Id == requestId);
        if (!string.IsNullOrEmpty(companyDb))
            query = query.Where(x => x.CompanyDb == companyDb);

        var row = await query
            .Select(x => new { x.Id, x.DocumentType, x.Action, x.RequestBody, x.SupportingData })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return NotFound(ApiResponse<object>.Fail(BaseErrorCodes.NullValue, "Approval request not found"));

        return Ok(ApiResponse<object>.Ok(new
        {
            row.Id,
            documentType = row.DocumentType.ToString(),
            action = row.Action.ToString(),
            requestBody = row.RequestBody,
            requestBodyJson = TryParseJson(row.RequestBody),
            supportingData = row.SupportingData,
            supportingDataJson = TryParseJson(row.SupportingData),
        }));
    }

    private static string? ResolvePlaintext(ApprovalCryptoRequest request)
    {
        if (request.Json is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } json)
            return json.GetRawText();

        return string.IsNullOrWhiteSpace(request.Text) ? null : request.Text;
    }

    private static object? TryParseJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ApiResponse<object> DecryptFailed() =>
        ApiResponse<object>.Fail(
            BaseErrorCodes.ValidationFailed,
            "Could not decrypt. Paste the AES ciphertext stored on ApprovalRequests.RequestBody (not RSA login ciphertext).");
}
