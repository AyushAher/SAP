using System.Text.RegularExpressions;
using FluentAssertions;

namespace SapApi.Tests.Services;

/// <summary>
/// The printed production order must stay a usable shop-floor document: order identity, product,
/// quantities, warehouses, customer/project, then the component lines and the signature block.
/// These guard the template against builder drift, which is what made it print raw placeholders.
/// </summary>
[TestFixture]
public class ProductionOrderTemplateTests
{
    private static string RepoFile(params string[] segments)
    {
        var root = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src"));
        return Path.Combine(new[] { root }.Concat(segments).ToArray());
    }

    private static string TemplateHtml(string project) =>
        File.ReadAllText(RepoFile("SapApi.Api", project, "production-order-template.html"));

    [TestCase("Templates")]
    [TestCase(@"wwwroot/Templates")]
    public void Every_template_placeholder_is_supplied_by_the_builder(string templateFolder)
    {
        var html = TemplateHtml(templateFolder);
        var builder = File.ReadAllText(RepoFile(
            "SapApi.Infrastructure", "Services", "ProductionOrders", "ProductionOrderPdfBuilder.cs"));

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
    public void Sections_follow_the_document_layout_order(string templateFolder)
    {
        var html = TemplateHtml(templateFolder);

        var order = new[]
        {
            "PRIVILEGE BIKSONS BOILERS PVT. LTD.",
            "Production Order</h3>",
            "Production Order No:",
            "Status:",
            "Customer:",
            "Project Details:",
            "Drawing No:",
            "Sales Order No:",
            "FG Product Details:",
            "Planned Quantity:",
            "Receipt Warehouse:",
            "Components</h3>",
            "{{@items}}",
            "Remarks:",
            "Prepared By",
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
    public void Document_shows_every_field_the_shop_floor_needs(string templateFolder)
    {
        var html = TemplateHtml(templateFolder);

        foreach (var key in new[]
        {
            "productionNo", "status", "orderDate", "startDate", "dueDate",
            "customerCode", "customerName", "projectCode", "projectName",
            "drawingNo", "productionCategory", "salesOrderNo",
            "productNo", "productName", "plannedQty", "completedQty", "rejectedQty", "uom",
            "receiptWarehouse", "issueWarehouse",
            "@items", "totalPlannedQty", "totalIssuedQty",
            "remarks", "userName", "printedOn",
        })
        {
            html.Should().Contain($"{{{{{key}}}}}");
        }
    }

    /// <summary>The component table's columns are what the legacy Blazor document printed.</summary>
    [TestCase("Templates")]
    [TestCase(@"wwwroot/Templates")]
    public void Component_table_keeps_its_columns(string templateFolder)
    {
        var html = TemplateHtml(templateFolder);

        var headers = new[] { "Sr. No", "Part No", "Description", "Planned Qty", "Issued Qty", "Warehouse", "UOM" };
        var positions = headers.Select(h =>
        {
            var index = html.IndexOf($">{h}</th>", StringComparison.Ordinal);
            index.Should().BeGreaterThanOrEqualTo(0, $"template should have a '{h}' column");
            return index;
        }).ToList();

        positions.Should().BeInAscendingOrder();
    }
}
