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
}
