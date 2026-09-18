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
        var html = await RenderTemplateHtmlAsync(templateName, placeholders, cancellationToken);
        return GeneratePdfFromHtml(html);
    }

    public async Task<string> RenderTemplateHtmlAsync(
        string templateName,
        IDictionary<string, string> placeholders,
        CancellationToken cancellationToken = default)
    {
        var templatePath = Path.Combine(env.ContentRootPath, "Templates", templateName);
        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Template not found: {templateName}");

        var html = await File.ReadAllTextAsync(templatePath, cancellationToken);
        foreach (var (key, value) in placeholders)
            html = html.Replace("{{" + key + "}}", value);

        // Every template can use {{copyrightYear}} in its footer without each builder having to
        // pass it — it's always just "this year", never document-specific data.
        html = html.Replace("{{copyrightYear}}", DateTime.UtcNow.Year.ToString());

        return html;
    }
}
