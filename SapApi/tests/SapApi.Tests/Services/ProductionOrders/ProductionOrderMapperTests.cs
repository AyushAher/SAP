using System.Text.Json;
using FluentAssertions;
using SapApi.Domain.Entities;
using SapApi.Infrastructure.Services.ProductionOrders;
using SapApi.Shared;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services.ProductionOrders;

[TestFixture]
public class ProductionOrderMapperTests
{
    private static readonly DateTime SyncedAt = new(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);

    [Test]
    public void ApplyHeader_copies_the_metadata_the_list_and_pickers_filter_on()
    {
        var entity = new ProductionOrder();

        ProductionOrderMapper.ApplyHeader(entity, new SapProductionOrdersResponse
        {
            AbsoluteEntry = 646,
            DocumentNumber = 10,
            Status = Constants.SapProductionOrderStatus.Planned,
            ItemNumber = "SF036770000",
            ProductDescription = "MEMBRANE PANEL",
            CustomerCode = "C000017",
            Project = "PB/R&M/25262053",
            ProjectName = "FORBESVYNCKE (PO NO:XX3824)",
            SalesOrderDocEntry = 512,
            SalesOrderDocNum = 252610128,
            Warehouse = "WIP",
            PlannedQuantity = 3,
            CompletedQuantity = 1,
            RejectedQuantity = 0,
            InventoryUom = "SET",
            DrawingNo = "4354d",
            ProductionCategory = "INT",
            DueDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        }, SyncedAt);

        entity.AbsoluteEntry.Should().Be(646);
        entity.DocumentNumber.Should().Be(10);
        entity.ItemNo.Should().Be("SF036770000");
        entity.ProductDescription.Should().Be("MEMBRANE PANEL");
        entity.CustomerCode.Should().Be("C000017");
        entity.Project.Should().Be("PB/R&M/25262053");
        entity.ProjectName.Should().Be("FORBESVYNCKE (PO NO:XX3824)");
        entity.SalesOrderDocEntry.Should().Be(512);
        entity.SalesOrderDocNum.Should().Be(252610128);
        entity.Warehouse.Should().Be("WIP");
        entity.PlannedQuantity.Should().Be(3);
        entity.CompletedQuantity.Should().Be(1);
        entity.InventoryUom.Should().Be("SET");
        entity.DrawingNo.Should().Be("4354d");
        entity.ProductionCategory.Should().Be("INT");
        entity.DueDate.Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        entity.SyncedAtUtc.Should().Be(SyncedAt);
    }

    [Test]
    public void ApplyHeader_keeps_a_previously_resolved_project_name_when_SAP_sends_none()
    {
        var entity = new ProductionOrder { ProjectName = "Resolved from project master" };

        ProductionOrderMapper.ApplyHeader(
            entity,
            new SapProductionOrdersResponse { AbsoluteEntry = 1, ProjectName = "   " },
            SyncedAt);

        entity.ProjectName.Should().Be("Resolved from project master");
    }

    [Test]
    public void ApplyHeader_revives_a_soft_deleted_row()
    {
        var entity = new ProductionOrder { IsDeleted = true };

        ProductionOrderMapper.ApplyHeader(
            entity,
            new SapProductionOrdersResponse { AbsoluteEntry = 1 },
            SyncedAt);

        entity.IsDeleted.Should().BeFalse();
    }

    [Test]
    public void MapLines_preserves_the_numeric_UoM_code_and_falls_back_to_the_index_for_line_numbers()
    {
        var lines = ProductionOrderMapper.MapLines(7,
        [
            new SapProductionOrderLines
            {
                LineNumber = 4,
                ItemNo = "RM-1",
                ItemName = "BEAM 200 MM",
                ItemType = "pit_Item",
                UoMCode = -1,
                UoMEntry = -1,
                PlannedQuantity = 135,
                IssuedQuantity = 10,
                Warehouse = "Store1",
                ProductionOrderIssueType = "im_Manual",
            },
            new SapProductionOrderLines { ItemNo = "RM-2", UoMCode = 12 },
        ]);

        lines.Should().HaveCount(2);
        lines[0].ProductionOrderId.Should().Be(7);
        lines[0].LineNumber.Should().Be(4);
        lines[0].ItemType.Should().Be("pit_Item");
        lines[0].UoMCode.Should().Be(-1);
        lines[0].PlannedQuantity.Should().Be(135);
        lines[0].IssuedQuantity.Should().Be(10);
        lines[0].ProductionOrderIssueType.Should().Be("im_Manual");
        lines[1].LineNumber.Should().Be(1);
        lines[1].UoMCode.Should().Be(12);
    }

    [TestCase("7", 7)]
    [TestCase(7, 7)]
    [TestCase(7.0, 7)]
    [TestCase("KG", null)]
    [TestCase(null, null)]
    public void ToUoMCode_only_accepts_a_whole_number(object? input, int? expected) =>
        ProductionOrderMapper.ToUoMCode(input).Should().Be(expected);

    [Test]
    public void ToUoMCode_reads_a_json_number_as_sent_by_the_service_layer()
    {
        var json = JsonDocument.Parse("{\"UoMCode\":-1}").RootElement.GetProperty("UoMCode");

        ProductionOrderMapper.ToUoMCode(json).Should().Be(-1);
    }

    [Test]
    public void ToDateTime_parses_the_service_layer_date_string()
    {
        ProductionOrderMapper.ToDateTime("2026-08-12").Should().Be(
            new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc));
        ProductionOrderMapper.ToDateTime("not-a-date").Should().BeNull();
        ProductionOrderMapper.ToDateTime(null).Should().BeNull();
    }

    [Test]
    public void ToSapResponse_round_trips_the_mirrored_header_and_lines()
    {
        var entity = new ProductionOrder
        {
            AbsoluteEntry = 646,
            DocumentNumber = 10,
            Status = Constants.SapProductionOrderStatus.Released,
            ItemNo = "SF036770000",
            ProductDescription = "MEMBRANE PANEL",
            CustomerCode = "C000017",
            CustomerName = "FORBESVYNCKE PRIVATE LIMITED",
            Project = "PB/R&M/25262053",
            ProjectName = "FORBESVYNCKE (PO NO:XX3824)",
            SalesOrderDocNum = 252610128,
            Warehouse = "WIP",
            PlannedQuantity = 3,
            Lines =
            [
                new ProductionOrderLine { LineNumber = 2, ItemNo = "RM-2", VisualOrder = 1, UoMCode = 7 },
                new ProductionOrderLine { LineNumber = 1, ItemNo = "RM-1", VisualOrder = 0, UoMCode = -1 },
            ],
        };

        var response = ProductionOrderMapper.ToSapResponse(entity, includeLines: true);

        response.AbsoluteEntry.Should().Be(646);
        response.CustomerName.Should().Be("FORBESVYNCKE PRIVATE LIMITED");
        response.ProjectName.Should().Be("FORBESVYNCKE (PO NO:XX3824)");
        response.SalesOrderDocNum.Should().Be(252610128);
        response.ProductionOrderLines.Should().NotBeNull();
        response.ProductionOrderLines!.Select(l => l.ItemNo).Should().Equal("RM-1", "RM-2");
        response.ProductionOrderLines[0].DocumentAbsoluteEntry.Should().Be(646);
        response.ProductionOrderLines[0].UoMCode.Should().Be(-1);
    }

    [Test]
    public void ToSapResponse_omits_lines_for_list_rows()
    {
        var response = ProductionOrderMapper.ToSapResponse(
            new ProductionOrder { AbsoluteEntry = 1, Lines = [new ProductionOrderLine { LineNumber = 1 }] },
            includeLines: false);

        response.ProductionOrderLines.Should().BeNull();
    }
}
