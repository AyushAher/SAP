using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Responses;

/// <summary>
/// The names a production order request body must use. [JsonPropertyName] governs reading as well as
/// writing and overrides the camelCase policy, so a body that spells these fields the friendly way
/// binds nothing and the fields reach SAP empty. The UI maps to these names before posting.
/// </summary>
[TestFixture]
public class ProductionOrderRequestBindingTests
{
    // Mirrors the controller pipeline configured in Program.cs.
    private static readonly JsonSerializerOptions BindingOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Test]
    public void SapNamedBody_BindsEveryFieldTheFormEdits()
    {
        const string body = """
        {
          "ItemNo": "FG-001",
          "ProductionOrderStatus": "boposReleased",
          "ProductionOrderType": "bopotDisassembly",
          "U_ProdType": "JOB",
          "U_DwgNo": "DWG-7",
          "ProductionOrderOriginNumber": 252610128,
          "ProductionOrderOriginEntry": 156,
          "PlannedQuantity": 5,
          "Warehouse": "Subcon",
          "DueDate": "2026-08-20",
          "StartDate": "2026-08-13",
          "PostingDate": "2026-08-13",
          "ProductionOrderLines": [
            { "ItemNo": "RM-100", "PlannedQuantity": 10, "Warehouse": "Store1" }
          ]
        }
        """;

        var bound = JsonSerializer.Deserialize<SapProductionOrdersResponse>(body, BindingOptions);

        bound.Should().NotBeNull();
        bound!.ItemNumber.Should().Be("FG-001");
        bound.Status.Should().Be("boposReleased");
        bound.Type.Should().Be("bopotDisassembly");
        bound.ProductionCategory.Should().Be("JOB");
        bound.DrawingNo.Should().Be("DWG-7");
        bound.SalesOrderDocNum.Should().Be(252610128);
        bound.SalesOrderDocEntry.Should().Be(156);
        bound.PlannedQuantity.Should().Be(5);
        bound.Warehouse.Should().Be("Subcon");
        bound.DueDate.Should().Be(new DateTime(2026, 8, 20));
        bound.StartDate.Should().Be(new DateTime(2026, 8, 13));
        bound.ProductionOrderLines!.Single().ItemNo.Should().Be("RM-100");
    }

    [Test]
    public void FriendlyNamedBody_BindsNothing()
    {
        const string body = """
        {
          "ItemNumber": "FG-001",
          "Status": "boposReleased",
          "Type": "bopotStandard",
          "ProductionCategory": "JOB",
          "DrawingNo": "DWG-7",
          "SalesOrderDocNum": 252610128,
          "SalesOrderDocEntry": 156
        }
        """;

        var bound = JsonSerializer.Deserialize<SapProductionOrdersResponse>(body, BindingOptions);

        bound.Should().NotBeNull();
        bound!.ItemNumber.Should().BeNull();
        bound.Status.Should().BeNull();
        bound.Type.Should().BeNull();
        bound.ProductionCategory.Should().BeNull();
        bound.DrawingNo.Should().BeNull();
        bound.SalesOrderDocNum.Should().BeNull();
        bound.SalesOrderDocEntry.Should().BeNull();
    }

    [Test]
    public void OmittingTheOptionalUserFields_LeavesThemNullSoAnUpdateDoesNotBlankThem()
    {
        var bound = JsonSerializer.Deserialize<SapProductionOrdersResponse>(
            """{ "ItemNo": "FG-001", "PlannedQuantity": 1 }""",
            BindingOptions);

        bound!.ProductionCategory.Should().BeNull();
        bound.DrawingNo.Should().BeNull();

        JsonSerializer.Serialize(bound).Should().NotContain("U_ProdType").And.NotContain("U_DwgNo");
    }
}
