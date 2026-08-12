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
