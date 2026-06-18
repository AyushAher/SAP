using System.Text;
using SapApi.Domain.Interfaces;
using SapApi.Shared.Configuration;
using SapApi.Shared.Models;

namespace SapApi.Api.Middleware;

public class SecurityMiddleware(RequestDelegate next)
{
    public const string EncryptedHeader = "X-Encrypted-Payload";
    public const string SignatureHeader = "X-Signature";

    public async Task InvokeAsync(
        HttpContext context,
        IRsaDecryptionService rsa,
        IHmacVerificationService hmac,
        Microsoft.Extensions.Options.IOptions<SecurityOptions> securityOptions)
    {
        if (HttpMethods.IsGet(context.Request.Method) ||
            context.Request.Path.StartsWithSegments("/swagger") ||
            context.Request.Path.StartsWithSegments("/api/auth/public-key"))
        {
            await next(context);
            return;
        }

        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        if (securityOptions.Value.RequireHmac)
        {
            var signature = context.Request.Headers[SignatureHeader].FirstOrDefault();
            if (!hmac.VerifySignature(body, signature ?? string.Empty))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("AUTH-02", "Invalid HMAC signature"));
                return;
            }
        }

        if (securityOptions.Value.RequireEncryptedPayload &&
            context.Request.Headers.ContainsKey(EncryptedHeader))
        {
            try
            {
                var decrypted = rsa.Decrypt(body.Trim('"'));
                var bytes = Encoding.UTF8.GetBytes(decrypted);
                context.Request.Body = new MemoryStream(bytes);
                context.Request.ContentLength = bytes.Length;
                context.Request.ContentType = "application/json";
            }
            catch
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("AUTH-03", "RSA decryption failed"));
                return;
            }
        }

        await next(context);
    }
}
