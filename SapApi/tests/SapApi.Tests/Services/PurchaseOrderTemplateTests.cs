using System.Text.RegularExpressions;
using FluentAssertions;

namespace SapApi.Tests.Services;

/// <summary>
/// The printed PO must keep the layout of the customer's sample: entity header, parties, order
/// meta, items, totals, terms, signatures. These guard the template against builder drift.
/// </summary>
[TestFixture]
public class PurchaseOrderTemplateTests
{
    private static string RepoFile(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src"));
        return Path.Combine(new[] { root }.Concat(segments).ToArray());
    }

    private static string TemplateHtml(string project) =>
        File.ReadAllText(RepoFile("SapApi.Api", project, "purchase-order-template.html"));

    [TestCase("Templates")]
    [TestCase(@"wwwroot/Templates")]
    public void Every_template_placeholder_is_supplied_by_the_builder(string templateFolder)
    {
        var html = TemplateHtml(templateFolder);
        var builder = File.ReadAllText(RepoFile(
            "SapApi.Infrastructure", "Services", "PurchaseOrders", "PurchaseOrderPdfBuilder.cs"));

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
    public void Sections_follow_the_sample_layout_order(string templateFolder)
    {
        var html = TemplateHtml(templateFolder);

        var order = new[]
        {
            "{{bplName}}",
            "PAN:</b> {{bplPan}}",
            "Purchase Order</td>",
            "Buy From",
            "Ship To",
            "Order No:",
            "Project Details:",
            "Reference:",
            "SR. NO.",
            "{{@items}}",
            "Amount in Figures",
            "Terms of Contract",
            "Prepared by:",
            "PO NO:",
            "Project:</b> {{projectDisplay}}",
            "Printed by:",
        };

        var positions = order.Select(marker =>
        {
            var index = html.IndexOf(marker, StringComparison.Ordinal);
            index.Should().BeGreaterThanOrEqualTo(0, $"template should contain '{marker}'");
            return index;
        }).ToList();

        positions.Should().BeInAscendingOrder();
    }

    [Test]
    public void Both_template_copies_stay_in_sync()
    {
        TemplateHtml("Templates").Should().Be(TemplateHtml(@"wwwroot/Templates"));
    }

    [TestCase("Templates")]
    [TestCase(@"wwwroot/Templates")]
    public void Qty_header_uses_the_purchase_unit_label_and_drops_the_system_unit_column(string templateFolder)
    {
        var html = TemplateHtml(templateFolder);
        html.Should().Contain("Qty in Purchase Unit");
        html.Should().NotContain("Qty in System Unit");
        html.Should().NotContain(">Purchase Qty<");
        html.Should().NotContain(">Stock Qty<");
    }

    [TestCase("Templates")]
    [TestCase(@"wwwroot/Templates")]
    public void Terms_font_matches_body_size(string templateFolder)
    {
        var html = TemplateHtml(templateFolder);
        var start = html.IndexOf(".terms {", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        html.Substring(start, 90).Should().Contain("font-size: 10px");
        html.Substring(start, 90).Should().NotContain("font-size: 8px");
    }

    [TestCase("Templates")]
    [TestCase(@"wwwroot/Templates")]
    public void Signature_names_sit_at_the_bottom_of_the_sign_row(string templateFolder)
    {
        var html = TemplateHtml(templateFolder);
        var signBlock = html.IndexOf(".sign td", StringComparison.Ordinal);
        signBlock.Should().BeGreaterThanOrEqualTo(0);
        html.Substring(signBlock, 80).Should().Contain("vertical-align: bottom");
    }

    [TestCase("Templates")]
    [TestCase(@"wwwroot/Templates")]
    public void Parties_show_the_full_tax_identity_block(string templateFolder)
    {
        var html = TemplateHtml(templateFolder);

        foreach (var key in new[]
        {
            "buyFromPin", "buyFromState", "buyFromStateCode", "buyFromPan", "buyFromGst",
            "shipToPin", "shipToState", "shipToStateCode", "shipToPan", "shipToGst",
        })
        {
            html.Should().Contain($"{{{{{key}}}}}");
        }
    }
}
