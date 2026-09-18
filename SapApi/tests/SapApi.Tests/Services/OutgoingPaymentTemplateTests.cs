using System.Text.RegularExpressions;
using FluentAssertions;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Responses;

namespace SapApi.Tests.Services;

[TestFixture]
public class OutgoingPaymentTemplateTests
{
    private static string RepoFile(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src"));
        return Path.Combine(new[] { root }.Concat(segments).ToArray());
    }

    private static string TemplateHtml(string project) =>
        File.ReadAllText(RepoFile("SapApi.Api", project, "outgoing-payment-template.html"));

    [TestCase("Templates")]
    [TestCase(@"wwwroot/Templates")]
    public void Every_template_placeholder_is_supplied_by_the_builder(string templateFolder)
    {
        var html = TemplateHtml(templateFolder);
        var builder = File.ReadAllText(RepoFile(
            "SapApi.Infrastructure", "Services", "StageWisePaymentPdfBuilder.cs"));

        var tokens = Regex.Matches(html, @"\{\{(?<key>[^}]+)\}\}")
            .Select(m => m.Groups["key"].Value)
            .Distinct()
            // copyrightYear is injected globally by PdfService.RenderTemplateHtmlAsync for every
            // template, not passed by individual builders.
            .Where(t => t != "copyrightYear")
            .ToList();
        tokens.Should().NotBeEmpty();

        var missing = tokens.Where(t => !builder.Contains($"[\"{t}\"]")).ToList();
        missing.Should().BeEmpty("the PDF would print raw placeholders for these keys");
    }

    [TestCase("Templates")]
    [TestCase(@"wwwroot/Templates")]
    public void Layout_keeps_prepared_by_and_printed_by_footer(string templateFolder)
    {
        var html = TemplateHtml(templateFolder);
        html.Should().Contain("Prepared by");
        html.Should().Contain("{{preparedBy}}");
        html.Should().Contain("Printed by:");
        html.Should().Contain("{{printedBy}}");
        html.Should().Contain("{{printedOn}}");
        html.Should().Contain("Vendor Bank Details:");
    }

    [Test]
    public void Both_template_copies_stay_in_sync()
    {
        TemplateHtml("Templates").Should().Be(TemplateHtml(@"wwwroot/Templates"));
    }

    [Test]
    public void FormatVendorBankDetails_joins_account_displays()
    {
        var text = StageWisePaymentPdfBuilder.FormatVendorBankDetails(
        [
            new VendorBankAccountOption { Display = "SBIN / 123 / Vendor" },
            new VendorBankAccountOption { Display = "HDFC / 456" },
        ]);
        text.Should().Be("SBIN / 123 / Vendor | HDFC / 456");
    }
}
