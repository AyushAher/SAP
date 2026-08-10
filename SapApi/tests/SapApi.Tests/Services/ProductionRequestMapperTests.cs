using FluentAssertions;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services;

[TestFixture]
public class ProductionRequestMapperTests
{
    private static SapInventoryGenExitRequestOrderLines ValidOrderLines(
        double issued = 1,
        double planned = 2) =>
        new()
        {
            ProductionOrder = new SapProductionOrdersResponse
            {
                AbsoluteEntry = 10,
                DocumentNumber = 100,
                CustomerCode = "C001",
                CustomerName = "Customer",
                Project = "P1",
                ProjectName = "Project One",
                Status = "boposReleased",
                ItemNumber = "FG-1",
                ProductDescription = "Finished Good",
            },
            ProductionOrderLinesEntryNumber =
            [
                new SapProductionOrderLines
                {
                    LineNumber = 0,
                    ItemNo = "RM-1",
                    PlannedQuantity = planned,
                    IssuedQuantity = issued,
                    Warehouse = "WH01",
                },
            ],
        };

    [Test]
    public void ValidateForSave_rejects_missing_production_order()
    {
        var orderLines = new SapInventoryGenExitRequestOrderLines
        {
            ProductionOrder = null!,
            ProductionOrderLinesEntryNumber = [new SapProductionOrderLines { PlannedQuantity = 1, IssuedQuantity = 1 }],
        };

        var act = () => ProductionRequestMapper.ValidateForSave(orderLines);

        act.Should().Throw<ArgumentException>().WithMessage("Production order is required.");
    }

    [Test]
    public void ValidateForSave_rejects_empty_lines()
    {
        var orderLines = ValidOrderLines();
        orderLines.ProductionOrderLinesEntryNumber = [];

        var act = () => ProductionRequestMapper.ValidateForSave(orderLines);

        act.Should().Throw<ArgumentException>()
            .WithMessage("At least one production order line is required.");
    }

    [Test]
    public void ValidateForSave_rejects_issued_above_planned()
    {
        var orderLines = ValidOrderLines(issued: 5, planned: 2);

        var act = () => ProductionRequestMapper.ValidateForSave(orderLines);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Issued quantity cannot exceed planned quantity for any line item.");
    }

    [Test]
    public void ToIssueEntity_and_ToReceiptEntity_share_validation_and_project_name()
    {
        var orderLines = ValidOrderLines();

        var issue = ProductionRequestMapper.ToIssueEntity(orderLines, "TEST_DB");
        var receipt = ProductionRequestMapper.ToReceiptEntity(orderLines, "TEST_DB");

        issue.CompanyDb.Should().Be("TEST_DB");
        receipt.CompanyDb.Should().Be("TEST_DB");
        issue.Project.Should().Be("P1");
        receipt.Project.Should().Be("P1");
        issue.ProjectName.Should().Be("Project One");
        receipt.ProjectName.Should().Be("Project One");
        issue.ItemNo.Should().Be("FG-1");
        receipt.ItemNo.Should().Be("FG-1");
        issue.RequestBody.Should().NotBeNullOrWhiteSpace();
        receipt.RequestBody.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void EnrichOrderLinesFromDraft_fills_blank_project_name()
    {
        var orderLines = ValidOrderLines();
        orderLines.ProductionOrder!.ProjectName = "";
        orderLines.ProductionOrder.CustomerName = "";

        var enriched = ProductionRequestMapper.EnrichOrderLinesFromDraft(
            orderLines,
            project: "P1",
            projectName: "Stored Project",
            cardCode: "C001",
            cardName: "Stored Customer",
            status: "boposReleased",
            itemNo: "FG-1",
            itemName: "Finished Good");

        enriched!.ProductionOrder!.ProjectName.Should().Be("Stored Project");
        enriched.ProductionOrder.CustomerName.Should().Be("Stored Customer");
    }

    [Test]
    public void ParseOrderLines_roundtrips_request_body()
    {
        var orderLines = ValidOrderLines();
        var entity = ProductionRequestMapper.ToReceiptEntity(orderLines, "TEST_DB");

        var parsed = ProductionRequestMapper.ParseOrderLines(entity.RequestBody);

        parsed.Should().NotBeNull();
        parsed!.ProductionOrder!.AbsoluteEntry.Should().Be(10);
        parsed.ProductionOrderLinesEntryNumber.Should().ContainSingle();
        parsed.ProductionOrderLinesEntryNumber[0].IssuedQuantity.Should().Be(1);
    }
}
