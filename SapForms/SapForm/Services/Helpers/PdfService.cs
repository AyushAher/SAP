using iText.Html2pdf;

namespace SapForm.Services.Helpers
{
    public class PdfService
    {
        public byte[] GeneratePdfFromHtml(string html)
        {
            using var ms = new MemoryStream();

            ConverterProperties props = new();

            HtmlConverter.ConvertToPdf(html, ms, props);

            return ms.ToArray();
        }

    }
}
