using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Responses.Sap;
using SapApi.Tests.Services.ProductionOrders;
using SapApi.Tests.Services.PurchaseOrders;

namespace SapApi.Tests.Services.Sap;

/// <summary>
/// What actually reaches SAP on a production order write. Both paths must go through the payload
/// preparation, because an approved request is replayed through create.
/// </summary>
[TestFixture]
public class SapProductionOrdersServiceWriteTests
{
    private const string CompanyDb = "PBBPL_UAT";
    private const int RequesterId = 1;

    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private Mock<IHttpRequestHandler> _http = null!;
    private SapProductionOrdersService _sut = null!;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _context.Users.Add(new ApplicationUser
        {
            Id = RequesterId,
            UserName = "user1@test.com",
            Email = "user1@test.com",
            NormalizedUserName = "USER1@TEST.COM",
            NormalizedEmail = "USER1@TEST.COM",
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _http = new Mock<IHttpRequestHandler>();

        var companyDbAccessor = new Mock<ICurrentCompanyDbAccessor>();
        companyDbAccessor.Setup(x => x.GetCompanyDbName()).Returns(CompanyDb);

        var claims = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, RequesterId.ToString())]);
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(claims) },
        };

        var purchaseOrderLinks = new PurchaseOrderLinkResolver(
            _context,
            companyDbAccessor.Object,
            PurchaseOrderLocalStoreTestHelper.Create(_context, _http.Object, companyDbAccessor.Object));
        var approvalService = new ApprovalService(
            _context,
            httpContextAccessor,
            companyDbAccessor.Object,
            purchaseOrderLinks);
        var localStore = ProductionOrderLocalStoreTestHelper.Create(
            _context,
            _http.Object,
            companyDbAccessor.Object,
            httpContextAccessor);

        _sut = new SapProductionOrdersService(_http.Object, approvalService, localStore);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static SapProductionOrdersResponse BuildOrder() => new()
    {
        ItemNumber = "FG-001",
        Status = "boposPlanned",
        Type = "bopotSpecial",
        ProductionCategory = "JOB",
        DrawingNo = "DWG-7",
        CustomerCode = "C000017",
        CustomerName = "Acme Industries",
        Project = "PRJ-1",
        ProjectName = "Refinery upgrade",
        Warehouse = "Subcon",
        PlannedQuantity = 5,
        SalesOrderDocNum = 252610128,
        SalesOrderDocEntry = 156,
        PostingDate = new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc),
        DueDate = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
        StartDate = new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc),
        ProductionOrderLines =
        [
            new SapProductionOrderLines
            {
                ItemNo = "RM-100",
                PlannedQuantity = 10,
                Warehouse = "Store1",
                UoMCode = "KG",
            },
        ],
        ProductionOrdersStages = [new SapProductionOrdersStage()],
    };

    [Test]
    public async Task CreateProductionOrder_SendsTheSapPropertyNamesForEveryHeaderField()
    {
        SapProductionOrdersResponse? posted = null;
        _http.Setup(h => h.PostAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, CancellationToken _) => posted = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 647, DocumentNumber = 11 });

        await _sut.CreateProductionOrderAsync(BuildOrder());

        posted.Should().NotBeNull();
        var json = JsonSerializer.Serialize(posted);

        json.Should().Contain("\"ItemNo\":\"FG-001\"");
        json.Should().Contain("\"ProductionOrderStatus\":\"boposPlanned\"");
        json.Should().Contain("\"ProductionOrderType\":\"bopotSpecial\"");
        json.Should().Contain("\"U_ProdType\":\"JOB\"");
        json.Should().Contain("\"U_DwgNo\":\"DWG-7\"");
        json.Should().Contain("\"ProductionOrderOriginNumber\":252610128");
        json.Should().Contain("\"ProductionOrderOriginEntry\":156");
        json.Should().Contain("\"Warehouse\":\"Subcon\"");
        json.Should().Contain("\"PlannedQuantity\":5");
        json.Should().NotContain("\"ItemNumber\"");
    }

    [Test]
    public async Task CreateProductionOrder_PreparesThePayloadTheSameWayAnUpdateDoes()
    {
        SapProductionOrdersResponse? posted = null;
        _http.Setup(h => h.PostAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, CancellationToken _) => posted = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 647, DocumentNumber = 11 });

        await _sut.CreateProductionOrderAsync(BuildOrder());

        posted.Should().NotBeNull();
        // U_CustomerName does not exist on OWOR, so SAP rejects the whole document if it is sent.
        posted!.CustomerName.Should().BeNull();
        posted.ProjectName.Should().BeNull();
        posted.ProductionOrdersStages.Should().BeNull();
        posted.ProductionOrdersSalesOrderLines.Should().BeNull();
        posted.ProductionOrdersDocumentReferences.Should().BeNull();
        // ProductionOrderLine.UoMCode is Edm.Int32; an inventory UoM name would be rejected.
        posted.ProductionOrderLines!.Single().UoMCode.Should().NotBe("KG");
        posted.ProductionOrderLines!.Single().VisualOrder.Should().Be(0);

        var json = JsonSerializer.Serialize(posted);
        json.Should().NotContain("U_CustomerName");
    }

    [Test]
    public async Task UpdateProductionOrder_StillPreparesThePayloadAndKeepsSapNames()
    {
        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PutAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 646, DocumentNumber = 10 });

        var order = BuildOrder();
        order.AbsoluteEntry = 646;
        order.Status = "boposReleased";

        await _sut.UpdateProductionOrderAsync(order);

        put.Should().NotBeNull();
        put!.CustomerName.Should().BeNull();
        put.ProductionOrderLines!.Single().DocumentAbsoluteEntry.Should().Be(646);

        var json = JsonSerializer.Serialize(put);
        json.Should().Contain("\"ProductionOrderStatus\":\"boposReleased\"");
        json.Should().Contain("\"AbsoluteEntry\":646");
    }

    [Test]
    public async Task PatchProductionOrderLine_SendsPatchWithSinglePreparedLine()
    {
        SapProductionOrdersResponse? patched = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, CancellationToken _) => patched = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 646 });

        var newLine = new SapProductionOrderLines
        {
            LineNumber = 2,
            VisualOrder = 1,
            ItemNo = "RM-200",
            PlannedQuantity = 3,
            Warehouse = "Store1",
            ProductionOrderIssueType = "im_Manual",
            UoMCode = "KG",
        };

        await _sut.PatchProductionOrderLineAsync(646, newLine);

        patched.Should().NotBeNull();
        patched!.ProductionOrderLines.Should().ContainSingle();
        var line = patched.ProductionOrderLines!.Single();
        line.ItemNo.Should().Be("RM-200");
        line.DocumentAbsoluteEntry.Should().Be(646);
        line.VisualOrder.Should().Be(1);
        line.ProductionOrderIssueType.Should().Be("im_Manual");
        line.UoMCode.Should().NotBe("KG");
        line.SerialNumbers.Should().BeNull();
        line.BatchNumbers.Should().BeNull();

        var json = JsonSerializer.Serialize(patched);
        json.Should().Contain("\"ProductionOrderLines\"");
        json.Should().NotContain("\"AbsoluteEntry\"");
        json.Should().NotContain("U_CustomerName");
    }

    [Test]
    public async Task CreateProductionOrder_OmitsTheOptionalUserFieldsWhenTheyAreNotSet()
    {
        SapProductionOrdersResponse? posted = null;
        _http.Setup(h => h.PostAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, CancellationToken _) => posted = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 647 });

        var order = BuildOrder();
        order.ProductionCategory = null;
        order.DrawingNo = null;

        await _sut.CreateProductionOrderAsync(order);

        var json = JsonSerializer.Serialize(posted);
        json.Should().NotContain("U_ProdType");
        json.Should().NotContain("U_DwgNo");
    }
}
