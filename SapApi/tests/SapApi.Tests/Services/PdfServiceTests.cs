using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using SapApi.Infrastructure.Services;

namespace SapApi.Tests.Services;

[TestFixture]
public class PdfServiceTests
{
    [Test]
    public void GeneratePdfFromHtml_ReturnsNonEmptyBytes()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(Directory.GetCurrentDirectory());
        var sut = new PdfService(env.Object);

        var result = sut.GeneratePdfFromHtml("<html><body><h1>Test PDF</h1></body></html>");
        result.Should().NotBeEmpty();
        result.Take(4).Should().Equal([0x25, 0x50, 0x44, 0x46]); // %PDF
    }

    [Test]
    public void Terms_joined_with_br_are_visible_in_the_rendered_pdf()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(Directory.GetCurrentDirectory());
        var sut = new PdfService(env.Object);

        var html = """
            <html><body>
            <table><tr><td>STANDARD TERMS &amp; CONDITIONS<br>1. Price Basis – Order is placed on ____ basis.</td></tr></table>
            </body></html>
            """;
        var bytes = sut.GeneratePdfFromHtml(html);
        using var reader = new iText.Kernel.Pdf.PdfReader(new MemoryStream(bytes));
        using var doc = new iText.Kernel.Pdf.PdfDocument(reader);
        var text = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(doc.GetFirstPage());

        text.Should().Contain("STANDARD TERMS");
        text.Should().Contain("Price Basis");
        text.Should().Contain("____");
    }
}
