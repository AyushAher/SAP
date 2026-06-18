namespace SapApi.Domain.Interfaces;

public interface IPdfService
{
    byte[] GeneratePdfFromHtml(string html);
    Task<byte[]> GeneratePdfFromTemplateAsync(string templateName, IDictionary<string, string> placeholders, CancellationToken cancellationToken = default);
}
