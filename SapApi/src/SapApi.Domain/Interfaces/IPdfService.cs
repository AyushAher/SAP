namespace SapApi.Domain.Interfaces;

public interface IPdfService
{
    byte[] GeneratePdfFromHtml(string html);
    Task<byte[]> GeneratePdfFromTemplateAsync(string templateName, IDictionary<string, string> placeholders, CancellationToken cancellationToken = default);

    /// <summary>Substitutes {{placeholder}} tokens in a Templates/ HTML file and returns the raw
    /// HTML — for callers that need the filled markup itself (e.g. an HTML email body) rather than
    /// a rendered PDF.</summary>
    Task<string> RenderTemplateHtmlAsync(string templateName, IDictionary<string, string> placeholders, CancellationToken cancellationToken = default);
}
