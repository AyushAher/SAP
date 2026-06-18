using iText.Html2pdf;
using Microsoft.Extensions.Hosting;
using SapApi.Domain.Interfaces;

namespace SapApi.Infrastructure.Services;

public class PdfService(IHostEnvironment env) : IPdfService
{
    public byte[] GeneratePdfFromHtml(string html)
    {
        using var ms = new MemoryStream();
        ConverterProperties props = new();
        HtmlConverter.ConvertToPdf(html, ms, props);
        return ms.ToArray();
    }

    public async Task<byte[]> GeneratePdfFromTemplateAsync(
        string templateName,
        IDictionary<string, string> placeholders,
        CancellationToken cancellationToken = default)
    {
        var templatePath = Path.Combine(env.ContentRootPath, "Templates", templateName);
        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"PDF template not found: {templateName}");

        var html = await File.ReadAllTextAsync(templatePath, cancellationToken);
        foreach (var (key, value) in placeholders)
            html = html.Replace("{{" + key + "}}", value);

        return GeneratePdfFromHtml(html);
    }
}
