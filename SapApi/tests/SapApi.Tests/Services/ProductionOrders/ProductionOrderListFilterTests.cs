using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services.ProductionOrders;
using SapApi.Shared;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;

namespace SapApi.Tests.Services.ProductionOrders;

/// <summary>
/// The mirrored list must page, filter and sort entirely in Postgres — the picker used to pull one
/// SAP page and filter it in the browser, which hid most released orders. Customer and project
/// names are stored on the row, so filtering by name never needs a live SAP lookup.
/// </summary>
[TestFixture]
public class ProductionOrderListFilterTests
{
    private const string CompanyDb = "PBBPL_UAT";

    private AppDbContext _context = null!;
    private Mock<IHttpRequestHandler> _http = null!;
    private ProductionOrderLocalStore _sut = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        _http = new Mock<IHttpRequestHandler>();

        var companyDb = new Mock<ICurrentCompanyDbAccessor>();
        companyDb.Setup(x => x.GetCompanyDbName()).Returns(CompanyDb);

        _sut = ProductionOrderLocalStoreTestHelper.Create(_context, _http.Object, companyDb.Object);
        SeedProductionOrders();
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private static PaginationRequest Request(
        int pageNumber = 1,
        int pageSize = 10,
        List<FilterModel>? filters = null,
        List<SortModel>? sorts = null) =>
        new()
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            Filters = filters ?? [],
            Sorts = sorts ?? [],
            IncludeTotalCount = true,
        };

    [Test]
    public async Task ListFromDb_defaults_to_newest_first_and_never_calls_SAP()
    {
        var page = await _sut.ListFromDbAsync(Request());

        page.Data!.Select(x => x.AbsoluteEntry).Should().Equal(104, 103, 102, 101);
        page.TotalCount.Should().Be(4);
        _http.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ListFromDb_pages_server_side()
    {
        var page = await _sut.ListFromDbAsync(Request(pageNumber: 2, pageSize: 2));

        page.Data!.Should().HaveCount(2);
        page.Data!.Select(x => x.AbsoluteEntry).Should().Equal(102, 101);
        page.TotalCount.Should().Be(4);
    }

    [Test]
    public async Task ListFromDb_matches_the_middle_of_a_customer_name_without_a_SAP_lookup()
    {
        var page = await _sut.ListFromDbAsync(Request(filters:
        [
            new FilterModel { Field = "customerName", Operator = "contains", Value = "VYNCK" },
        ]));

        page.Data!.Select(x => x.AbsoluteEntry).Should().Equal(101);
        _http.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ListFromDb_searches_customer_code_and_name_through_one_alias()
    {
        var byCode = await _sut.ListFromDbAsync(Request(filters:
        [
            new FilterModel { Field = "customerName", Operator = "contains", Value = "C000125" },
        ]));

        byCode.Data!.Select(x => x.AbsoluteEntry).Should().Equal(102);
    }

    [Test]
    public async Task ListFromDb_searches_item_code_and_description_through_one_alias()
    {
        var byDescription = await _sut.ListFromDbAsync(Request(filters:
        [
            new FilterModel { Field = "itemNumber", Operator = "contains", Value = "TUBESHEET" },
        ]));

        byDescription.Data!.Select(x => x.AbsoluteEntry).Should().Equal(103);

        var byCode = await _sut.ListFromDbAsync(Request(filters:
        [
            new FilterModel { Field = "itemNumber", Operator = "contains", Value = "SF0367" },
        ]));

        byCode.Data!.Select(x => x.AbsoluteEntry).Should().Equal(101);
    }

    [Test]
    public async Task ListFromDb_matches_the_middle_of_a_project_name()
    {
        var page = await _sut.ListFromDbAsync(Request(filters:
        [
            new FilterModel { Field = "project", Operator = "contains", Value = "BIOREFINER" },
        ]));

        page.Data!.Select(x => x.AbsoluteEntry).Should().Equal(102);
    }

    [Test]
    public async Task ListFromDb_filters_released_orders_for_the_production_pickers()
    {
        var page = await _sut.ListFromDbAsync(Request(filters:
        [
            new FilterModel
            {
                Field = "Status",
                Operator = "eq",
                Value = Constants.SapProductionOrderStatus.Released,
            },
        ]));

        page.Data!.Select(x => x.AbsoluteEntry).Should().Equal(104, 102);
        page.TotalCount.Should().Be(2);
    }

    [Test]
    public async Task ListFromDb_maps_ui_column_aliases_onto_mirror_columns()
    {
        var page = await _sut.ListFromDbAsync(Request(
            filters:
            [
                new FilterModel
                {
                    Field = "ProductionOrderStatus",
                    Operator = "eq",
                    Value = Constants.SapProductionOrderStatus.Released,
                },
            ],
            sorts: [new SortModel { Field = "DocNum", Direction = "asc" }]));

        page.Data!.Select(x => x.DocumentNumber).Should().Equal(900102, 900104);
    }

    [Test]
    public async Task ListFromDb_sorts_server_side_on_a_requested_column()
    {
        var page = await _sut.ListFromDbAsync(Request(
            sorts: [new SortModel { Field = "PlannedQuantity", Direction = "desc" }]));

        page.Data!.Select(x => x.PlannedQuantity).Should().Equal(100, 12, 5, 1);
    }

    [Test]
    public async Task ListFromDb_never_returns_another_company_database()
    {
        _context.ProductionOrders.Add(new ProductionOrder
        {
            CompanyDb = "OTHER_DB",
            AbsoluteEntry = 999,
            DocumentNumber = 999,
            SyncedAtUtc = DateTime.UtcNow,
        });
        _context.SaveChanges();

        var page = await _sut.ListFromDbAsync(Request());

        page.Data!.Should().NotContain(x => x.AbsoluteEntry == 999);
    }

    [Test]
    public async Task GetFromDb_returns_the_order_with_its_lines()
    {
        var order = await _sut.GetFromDbAsync(101, includeLines: true);

        order.Should().NotBeNull();
        order!.ProductionOrderLines.Should().HaveCount(2);
        order.ProductionOrderLines![0].ItemNo.Should().Be("RM-1");
        _http.VerifyNoOtherCalls();
    }

    [Test]
    public async Task GetLinesFromDb_orders_lines_by_visual_order()
    {
        var lines = await _sut.GetLinesFromDbAsync(101);

        lines.Select(l => l.ItemNo).Should().Equal("RM-1", "RM-2");
        lines.Select(l => l.UoMCode).Should().Equal(-1, 7);
    }

    private void SeedProductionOrders()
    {
        var now = DateTime.UtcNow;
        _context.ProductionOrders.AddRange(
            new ProductionOrder
            {
                CompanyDb = CompanyDb,
                AbsoluteEntry = 101,
                DocumentNumber = 900101,
                Status = Constants.SapProductionOrderStatus.Planned,
                ItemNo = "SF036770000",
                ProductDescription = "MEMBRANE PANEL - RADIATION ZONE",
                CustomerCode = "C000017",
                CustomerName = "FORBESVYNCKE PRIVATE LIMITED",
                Project = "PB/R&M/25262053",
                ProjectName = "FORBESVYNCKE (PO NO:XX3824)",
                Warehouse = "WIP",
                PlannedQuantity = 1,
                SyncedAtUtc = now,
                Lines =
                [
                    new ProductionOrderLine { LineNumber = 1, ItemNo = "RM-1", VisualOrder = 0, UoMCode = -1 },
                    new ProductionOrderLine { LineNumber = 2, ItemNo = "RM-2", VisualOrder = 1, UoMCode = 7 },
                ],
            },
            new ProductionOrder
            {
                CompanyDb = CompanyDb,
                AbsoluteEntry = 102,
                DocumentNumber = 900102,
                Status = Constants.SapProductionOrderStatus.Released,
                ItemNo = "SF555040000",
                ProductDescription = "SPENTWASH TANK",
                CustomerCode = "C000125",
                CustomerName = "GODAVARI LIMITED",
                Project = "PB25-26252610023",
                ProjectName = "GODAVARI BIOREFINERIES LIMITED",
                Warehouse = "WIP",
                PlannedQuantity = 12,
                SyncedAtUtc = now,
            },
            new ProductionOrder
            {
                CompanyDb = CompanyDb,
                AbsoluteEntry = 103,
                DocumentNumber = 900103,
                Status = Constants.SapProductionOrderStatus.Closed,
                ItemNo = "SF020170000",
                ProductDescription = "APH TUBESHEET",
                Warehouse = "WIP",
                PlannedQuantity = 100,
                SyncedAtUtc = now,
            },
            new ProductionOrder
            {
                CompanyDb = CompanyDb,
                AbsoluteEntry = 104,
                DocumentNumber = 900104,
                Status = Constants.SapProductionOrderStatus.Released,
                ItemNo = "SF999990000",
                ProductDescription = "ECONOMISER COIL",
                Warehouse = "STORE1",
                PlannedQuantity = 5,
                SyncedAtUtc = now,
            });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }
}
