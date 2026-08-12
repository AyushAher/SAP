using FluentAssertions;
using SapApi.Infrastructure.Services.ProductionOrders;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services.ProductionOrders;

/// <summary>
/// The builder is fed the mirrored order exactly as the local store returns it, so these tests
/// also pin the promise that printing needs nothing beyond the mirror.
/// </summary>
[TestFixture]
public class ProductionOrderPdfBuilderTests
{
    private static SapProductionOrdersResponse MirroredOrder() => new()
    {
        AbsoluteEntry = 646,
        DocumentNumber = 10,
        ItemNumber = "SF036770000",
        ProductDescription = "MEMBRANE PANEL - RADIATION ZONE",
        Status = "boposPlanned",
        ProductionCategory = "INT",
        DrawingNo = "4354d",
        PlannedQuantity = 1,
        CompletedQuantity = 0,
        RejectedQuantity = 0,
        Warehouse = "WIP",
        InventoryUom = "SET",
        CustomerCode = "C000017",
        CustomerName = "FORBES & COMPANY LIMITED",
        Project = "PB/R&M/25262053",
        ProjectName = "FORBESVYNCKE (PO NO:XX3824)",
        SalesOrderDocNum = 252610128,
        CreationDate = new DateTime(2026, 6, 16),
        StartDate = new DateTime(2026, 6, 18),
        DueDate = new DateTime(2026, 6, 27),
        ProductionOrderLines =
        [
            new SapProductionOrderLines
            {
                LineNumber = 1,
                ItemNo = "RM5703810600380",
                ItemName = "BEAM 200 MM X 100 MM IS 2062 E250",
                PlannedQuantity = 135,
                IssuedQuantity = 0,
                Warehouse = "Store1",
                UoMCode = -1,
            },
            new SapProductionOrderLines
            {
                LineNumber = 2,
                ItemNo = "RM5708606200380",
                ItemName = "CHANNEL 150 MM X 75 MM",
                PlannedQuantity = 10,
                IssuedQuantity = 2.5,
                Warehouse = "PBPL(S)",
                UoMCode = -1,
                FreeText = "cut to 3.2 m",
            },
        ],
    };

    private static Dictionary<string, string> Build(SapProductionOrdersResponse order, string? userName = "Ayush Aher") =>
        new ProductionOrderPdfBuilder().BuildPlaceholders(order, userName);

    [Test]
    public void Header_reproduces_the_mirrored_order()
    {
        var result = Build(MirroredOrder());

        result["productionNo"].Should().Be("10");
        result["status"].Should().Be("Planned");
        result["orderDate"].Should().Be("16/06/2026");
        result["startDate"].Should().Be("18/06/2026");
        result["dueDate"].Should().Be("27/06/2026");
        result["productionCategory"].Should().Be("INT");
        result["drawingNo"].Should().Be("4354d");
        result["salesOrderNo"].Should().Be("252610128");
        result["productNo"].Should().Be("SF036770000");
        result["productName"].Should().Be("MEMBRANE PANEL - RADIATION ZONE");
        result["plannedQty"].Should().Be("1.00");
        result["completedQty"].Should().Be("0.00");
        result["rejectedQty"].Should().Be("0.00");
        result["uom"].Should().Be("SET");
        result["receiptWarehouse"].Should().Be("WIP");
        result["userName"].Should().Be("Ayush Aher");
    }

    /// <summary>Codes alone are useless on paper; the mirror stores the resolved names.</summary>
    [Test]
    public void Customer_and_project_print_their_resolved_names()
    {
        var result = Build(MirroredOrder());

        result["customerCode"].Should().Be("C000017");
        result["customerName"].Should().Be("FORBES &amp; COMPANY LIMITED");
        result["projectCode"].Should().Be("PB/R&amp;M/25262053");
        result["projectName"].Should().Be("FORBESVYNCKE (PO NO:XX3824)");
    }

    [Test]
    public void Component_lines_carry_code_description_quantities_and_warehouse()
    {
        var result = Build(MirroredOrder());
        var items = result["@items"];

        items.Should().Contain("RM5703810600380");
        items.Should().Contain("BEAM 200 MM X 100 MM IS 2062 E250");
        items.Should().Contain("CHANNEL 150 MM X 75 MM - cut to 3.2 m");
        items.Should().Contain("135.00");
        items.Should().Contain("2.50");
        items.Should().Contain("Store1");
        items.Should().Contain("PBPL(S)");
        result["totalPlannedQty"].Should().Be("145.00");
        result["totalIssuedQty"].Should().Be("2.50");
    }

    /// <summary>SAP's numeric WOR1 UoMCode is not a unit name and must never be printed as one.</summary>
    [TestCase(-1)]
    [TestCase(6)]
    [TestCase("6")]
    public void Numeric_line_uom_code_is_never_printed(object uomCode)
    {
        var order = MirroredOrder();
        order.ProductionOrderLines![0].UoMCode = uomCode;

        var items = Build(order)["@items"];

        items.Should().NotContain($">{uomCode}<");
    }

    [Test]
    public void Named_line_uom_is_printed_when_the_mirror_has_one()
    {
        var order = MirroredOrder();
        order.ProductionOrderLines![0].UoMCode = "KG";

        Build(order)["@items"].Should().Contain(">KG<");
    }

    /// <summary>SAP has no issue warehouse on the header, so the components' stores are printed.</summary>
    [Test]
    public void Issue_warehouse_lists_the_component_warehouses()
    {
        Build(MirroredOrder())["issueWarehouse"].Should().Be("Store1, PBPL(S)");
    }

    [Test]
    public void Order_without_lines_prints_an_explicit_empty_row()
    {
        var order = MirroredOrder();
        order.ProductionOrderLines = [];

        var result = Build(order);

        result["@items"].Should().Contain("No component lines");
        result["totalPlannedQty"].Should().Be("0.00");
        result["issueWarehouse"].Should().Be("-");
    }

    [Test]
    public void Missing_values_print_a_dash_so_no_cell_looks_lost()
    {
        var order = new SapProductionOrdersResponse { AbsoluteEntry = 700 };

        var result = Build(order, userName: null);

        result["productionNo"].Should().Be("700");
        result["customerName"].Should().Be("-");
        result["drawingNo"].Should().Be("-");
        result["dueDate"].Should().Be("-");
        result["orderDate"].Should().Be("-");
        result["remarks"].Should().Be("-");
        result["userName"].Should().Be("-");
    }

    [Test]
    public void Every_placeholder_has_a_value_and_no_placeholder_leaks_through()
    {
        var result = Build(MirroredOrder());

        result.Should().NotContainValue(string.Empty);
        result.Values.Should().NotContain(v => v.Contains("{{"));
    }
}
