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
}
