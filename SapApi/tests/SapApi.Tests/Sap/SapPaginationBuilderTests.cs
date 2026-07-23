using FluentAssertions;
using SapApi.Infrastructure.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Tests.Sap;

[TestFixture]
public class SapPaginationBuilderTests
{
    [Test]
    public void ToSapQueries_BuildsSkipTopFilterWithoutInlineCountByDefault()
    {
        var request = new PaginationRequest
        {
            PageNumber = 2,
            PageSize = 10,
            Filters =
            [
                new() { Field = "CardName", Operator = "contains", Value = "Acme" },
            ],
            Sorts = [new() { Field = "DocNum", Direction = "desc" }],
        };

        var query = SapPaginationBuilder.ToSapQueries(request, SapPaginationProfiles.PurchaseOrders);

        query.Top.Should().Be("10");
        query.Skip.Should().Be("10");
        query.InlineCount.Should().BeFalse();
        query.Filter.Should().Contain("DocDate ge '2026-01-01'");
        query.Filter.Should().Contain("startswith(CardName,'Acme')");
        query.OrderBy.Should().Be("DocNum desc");
        query.GetQueryValue().Should().NotContain("$inlinecount=allpages");
    }

    [Test]
    public void ToSapQueries_IncludesInlineCountWhenRequested()
    {
        var request = new PaginationRequest
        {
            PageNumber = 1,
            PageSize = 10,
            IncludeTotalCount = true,
        };

        var query = SapPaginationBuilder.ToSapQueries(request, SapPaginationProfiles.Items);
        query.InlineCount.Should().BeTrue();
        query.GetQueryValue().Should().Contain("$inlinecount=allpages");
    }

    [Test]
    public void BuildSearchFilter_UsesExactMatchForMasterCodes()
    {
        var request = new PaginationRequest
        {
            Filters = [new FilterModel { Field = "__search", Operator = "contains", Value = "FG-001" }],
        };

        var query = SapPaginationBuilder.ToSapQueries(request, SapPaginationProfiles.Items);
        query.Filter.Should().Contain("ItemCode eq 'FG-001'");
        query.Filter.Should().NotContain("contains(");
    }

    [Test]
    public void ResolveTotalCount_UsesODataCountWhenPresent()
    {
        var response = new GetAllSapPurchaseOrdersResponse { ODataCount = 42 };
        var total = SapPaginationBuilder.ResolveTotalCount(response, new List<string> { "a" }, new PaginationRequest { PageNumber = 1, PageSize = 10 });
        total.Should().Be(42);
    }

    [Test]
    public void ResolveSelect_NoFieldsRequested_ReturnsDefaultSelectUnchanged()
    {
        var select = SapPaginationBuilder.ResolveSelect("ItemCode,ItemName,InventoryUOM", ["ItemCode"], null);
        select.Should().Be("ItemCode,ItemName,InventoryUOM");
    }

    [Test]
    public void ResolveSelect_RequestedSubset_NarrowsToRequestedFieldsPlusKeyFields()
    {
        var select = SapPaginationBuilder.ResolveSelect("ItemCode,ItemName,InventoryUOM", ["ItemCode"], ["ItemName"]);
        select.Should().Be("ItemCode,ItemName");
    }

    [Test]
    public void ResolveSelect_KeyFieldAlwaysIncludedEvenIfNotRequested()
    {
        var select = SapPaginationBuilder.ResolveSelect("CardCode,CardName,CardType", ["CardCode"], ["CardType"]);
        select.Should().Be("CardCode,CardType");
    }

    [Test]
    public void ResolveSelect_UnknownRequestedFieldsAreIgnored_CannotExpandBeyondDefaultSelect()
    {
        // "SecretField" isn't part of the default select, so it must never leak into the resolved
        // $select — a caller can only narrow the field set, never widen it.
        var select = SapPaginationBuilder.ResolveSelect("ItemCode,ItemName", ["ItemCode"], ["ItemName", "SecretField"]);
        select.Should().Be("ItemCode,ItemName");
        select.Should().NotContain("SecretField");
    }

    [Test]
    public void ResolveSelect_EmptyRequestedFieldsList_ReturnsDefaultSelectUnchanged()
    {
        var select = SapPaginationBuilder.ResolveSelect("ItemCode,ItemName", ["ItemCode"], []);
        select.Should().Be("ItemCode,ItemName");
    }

    [Test]
    public void ResolveSelect_RequestedFieldsMatchNothingInDefault_FallsBackToDefaultSelect()
    {
        var select = SapPaginationBuilder.ResolveSelect("Code,Name", [], ["DoesNotExist"]);
        select.Should().Be("Code,Name");
    }

    [Test]
    public void ToSapQueries_WithRequestedFields_NarrowsSelectInBuiltQuery()
    {
        var request = new PaginationRequest
        {
            PageNumber = 1,
            PageSize = 10,
            Fields = ["ItemName"],
        };

        var query = SapPaginationBuilder.ToSapQueries(request, SapPaginationProfiles.Items);

        query.Select.Should().Be("ItemCode,ItemName");
    }
}
