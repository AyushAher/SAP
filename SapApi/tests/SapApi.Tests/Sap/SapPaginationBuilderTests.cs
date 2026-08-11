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
        query.Filter.Should().Contain("contains(CardName,'Acme')");
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
        // Code-like terms still get contains on text/name fields so mid-string name search works.
        query.Filter.Should().Contain("contains(ItemName,'FG-001')");
    }

    [Test]
    public void BuildSearchFilter_Project_UsesContainsOnCodeAndNameForAnyKeyword()
    {
        // Any keyword should mid-string match both Code and Name (not code-prefix / exact only).
        var request = new PaginationRequest
        {
            Filters = [new FilterModel { Field = "__search", Operator = "contains", Value = "SOMESHWAR" }],
        };

        var query = SapPaginationBuilder.ToSapQueries(request, SapPaginationProfiles.Projects);
        query.Filter.Should().Contain("contains(Name,'SOMESHWAR')");
        query.Filter.Should().Contain("contains(Code,'SOMESHWAR')");
        query.Filter.Should().NotContain("startswith(Name,'SOMESHWAR')");
        query.Filter.Should().NotContain("Code eq 'SOMESHWAR'");
    }

    [Test]
    public void BuildSearchFilter_UsesUnquotedExactMatchForNumericCodeFields()
    {
        var request = new PaginationRequest
        {
            Filters = [new FilterModel { Field = "__search", Operator = "contains", Value = "18" }],
        };

        var salesQuery = SapPaginationBuilder.ToSapQueries(request, SapPaginationProfiles.SalesPersons);
        salesQuery.Filter.Should().Contain("SalesEmployeeCode eq 18");
        salesQuery.Filter.Should().NotContain("SalesEmployeeCode eq '18'");

        var employeeQuery = SapPaginationBuilder.ToSapQueries(request, SapPaginationProfiles.EmployeesInfo);
        employeeQuery.Filter.Should().Contain("EmployeeID eq 18");
        employeeQuery.Filter.Should().NotContain("EmployeeID eq '18'");
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
