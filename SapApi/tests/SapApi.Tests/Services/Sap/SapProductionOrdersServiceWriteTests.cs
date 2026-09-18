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
using SapApi.Shared;
using SapApi.Shared.Requests;
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
        json.Should().Contain("\"U_PrjName\":\"Refinery upgrade\"");
        json.Should().NotContain("\"ParentProductionOrderNo\"");
        posted!.ParentProductionOrderNo.Should().BeNull();
        posted.ProductionOrderLines!.Single().DocNum.Should().BeNull();
        json.Should().NotContain("U_DocNum");
        json.Should().Contain("\"ProductionOrderOriginNumber\":252610128");
        json.Should().Contain("\"ProductionOrderOriginEntry\":156");
        json.Should().Contain("\"Warehouse\":\"Subcon\"");
        json.Should().Contain("\"PlannedQuantity\":5");
        json.Should().NotContain("\"ItemNumber\"");
        json.Should().NotContain("\"BaseQuantity\"");
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
        posted.ProjectName.Should().Be("Refinery upgrade");
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
        IReadOnlyDictionary<string, string>? headers = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> h, CancellationToken _) =>
            {
                put = body;
                headers = h;
            })
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 646, DocumentNumber = 10 });

        var order = BuildOrder();
        order.AbsoluteEntry = 646;
        order.Status = "boposReleased";

        await _sut.UpdateProductionOrderAsync(order);

        put.Should().NotBeNull();
        put!.CustomerName.Should().BeNull();
        put.ProductionOrderLines!.Single().DocumentAbsoluteEntry.Should().Be(646);
        headers.Should().ContainKey(Constants.SapServiceLayerHeaders.ReplaceCollectionsOnPatch)
            .WhoseValue.Should().Be("true");
        _http.Verify(
            h => h.PutAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        var json = JsonSerializer.Serialize(put);
        json.Should().Contain("\"ProductionOrderStatus\":\"boposReleased\"");
        json.Should().Contain("\"AbsoluteEntry\":646");
    }

    [Test]
    public async Task UpdateProductionOrder_HeaderOnlyKeepsExistingComponentsAndOmitsCalculatedHeaderFields()
    {
        await SeedParentAsync();

        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 646, DocumentNumber = 10 });

        var order = BuildOrder();
        order.AbsoluteEntry = 646;
        order.Remarks = "Header only";
        order.CompletedQuantity = 1;
        order.Priority = 100;
        order.CreationDate = new DateTime(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc);
        order.ProductionOrderLines = [];

        await _sut.UpdateProductionOrderAsync(order);

        put.Should().NotBeNull();
        put!.Remarks.Should().Be("Header only");
        put.ProductionOrderLines.Should().ContainSingle(l => l.ItemNo == "RM-BOM");
        put.ProductionOrderLines!.Should().OnlyContain(l => l.LineNumber == 0 && l.IssuedQuantity == null);
        put.ProductionOrderLines.Single().BaseQuantity.Should().Be(12);
        put.CompletedQuantity.Should().BeNull();
        put.Priority.Should().BeNull();
        put.CreationDate.Should().BeNull();
        put.PostingDate.Should().Be(new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc));
        var json = JsonSerializer.Serialize(put);
        json.Should().NotContain("CompletedQuantity");
        json.Should().NotContain("\"Priority\"");
        json.Should().NotContain("CreationDate");
    }

    [Test]
    public async Task UpdateProductionOrder_HeaderOnlyKeepsIssuedLineIdentity()
    {
        await SeedParentAsync();
        var parentRow = await _context.ProductionOrders
            .AsTracking()
            .Include(x => x.Lines)
            .SingleAsync(x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == 646);
        parentRow.Status = "boposReleased";
        parentRow.Lines.Single().IssuedQuantity = 1;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 646, DocumentNumber = 10 });

        var order = BuildOrder();
        order.AbsoluteEntry = 646;
        order.Status = "boposReleased";
        order.CompletedQuantity = 1;
        order.Remarks = "Header only issued";
        order.ProductionOrderLines = [];

        await _sut.UpdateProductionOrderAsync(order);

        put.Should().NotBeNull();
        var line = put!.ProductionOrderLines.Should().ContainSingle().Subject;
        line.ItemNo.Should().Be("RM-BOM");
        line.LineNumber.Should().Be(0);
        line.IssuedQuantity.Should().BeNull();
        line.BaseQuantity.Should().Be(12);
        put.CompletedQuantity.Should().BeNull();
        JsonSerializer.Serialize(put).Should().NotContain("CompletedQuantity");
    }

    [Test]
    public async Task UpdateProductionOrder_HeaderOnlyKeepsLiveTypePostingDateAndItem()
    {
        await SeedParentAsync();

        _http.Setup(h => h.GetOrThrowAsync<SapProductionOrdersResponse>(
                It.Is<string>(url => url.Contains("ProductionOrders(646)")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapProductionOrdersResponse
            {
                AbsoluteEntry = 646,
                DocumentNumber = 10,
                ItemNumber = "SF020170000",
                Status = "boposReleased",
                Type = "bopotStandard",
                Warehouse = "WIP",
                PlannedQuantity = 1,
                PostingDate = new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc),
                DueDate = new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc),
                StartDate = new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc),
                ProductionOrderOrigin = "bopooManual",
                ProductionOrderLines =
                [
                    new SapProductionOrderLines
                    {
                        LineNumber = 3,
                        ItemNo = "RM45351000012038225",
                        PlannedQuantity = 50,
                        BaseQuantity = 50,
                        IssuedQuantity = 50,
                        Warehouse = "Store1",
                        LocationCode = 2,
                    },
                ],
            });

        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 646, DocumentNumber = 10 });

        var order = BuildOrder();
        order.AbsoluteEntry = 646;
        order.ItemNumber = "FG-001";
        order.Type = "bopotSpecial";
        order.Warehouse = "Subcon";
        order.Status = "boposReleased";
        order.PlannedQuantity = 1;
        order.PostingDate = DateTime.UtcNow.Date;
        order.Remarks = "Header overlay";
        order.ProductionOrderLines = [];

        await _sut.UpdateProductionOrderAsync(order);

        put.Should().NotBeNull();
        put!.ItemNumber.Should().Be("SF020170000");
        put.Type.Should().Be("bopotStandard");
        put.Warehouse.Should().Be("WIP");
        put.PostingDate.Should().Be(new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc));
        put.Remarks.Should().Be("Header overlay");
        put.Status.Should().Be("boposReleased");
        var line = put.ProductionOrderLines.Should().ContainSingle().Subject;
        line.LineNumber.Should().Be(3);
        line.ItemNo.Should().Be("RM45351000012038225");
        line.IssuedQuantity.Should().BeNull();
        line.LocationCode.Should().Be(2);
        JsonSerializer.Serialize(put).Should().NotContain("CompletedQuantity");
    }

    [Test]
    public async Task UpdateProductionOrder_OmitsManualUomPlaceholderSoSapCanCommit()
    {
        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 661, DocumentNumber = 25 });

        var order = BuildOrder();
        order.AbsoluteEntry = 661;
        order.UoMEntry = -1;
        order.ProductionOrderLines =
        [
            new SapProductionOrderLines
            {
                ItemNo = "RM5703806200380",
                PlannedQuantity = 12,
                Warehouse = "Store1",
                UoMEntry = -1,
                UoMCode = -1,
            },
        ];

        await _sut.UpdateProductionOrderAsync(order);

        put.Should().NotBeNull();
        put!.UoMEntry.Should().BeNull();
        put.ProductionOrderLines!.Single().UoMCode.Should().BeNull();
        put.ProductionOrderLines.Single().UoMEntry.Should().BeNull();
        var json = JsonSerializer.Serialize(put);
        json.Should().NotContain("UoMCode");
        json.Should().NotContain("UoMEntry");
    }

    [Test]
    public async Task UpdateProductionOrder_AppendsNewLinesOnTheIssuingWarehouseWithoutInventedLineNumbers()
    {
        var now = DateTime.UtcNow;
        _context.ProductionOrders.Add(new ProductionOrder
        {
            CompanyDb = CompanyDb,
            AbsoluteEntry = 661,
            DocumentNumber = 25,
            Warehouse = "WIP",
            SyncedAtUtc = now,
            CreatedOn = now,
            LastModifiedOn = now,
            Lines =
            [
                new ProductionOrderLine
                {
                    LineNumber = 0,
                    ItemNo = "RM5703806200380",
                    Warehouse = "Store1",
                    LocationCode = 2,
                },
            ],
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 661, DocumentNumber = 25 });

        var order = BuildOrder();
        order.AbsoluteEntry = 661;
        order.Warehouse = "WIP";
        order.ProductionOrderLines =
        [
            new SapProductionOrderLines
            {
                LineNumber = 0,
                ItemNo = "RM5703806200380",
                PlannedQuantity = 12,
                Warehouse = "Store1",
                LocationCode = 2,
                ProductionOrderIssueType = "im_Manual",
            },
            new SapProductionOrderLines
            {
                LineNumber = 1,
                ItemNo = "RM5708606200380",
                PlannedQuantity = 12,
                Warehouse = "WIP",
            },
        ];

        await _sut.UpdateProductionOrderAsync(order);

        put.Should().NotBeNull();
        put!.ProductionOrderLines.Should().HaveCount(2);

        var existing = put.ProductionOrderLines![0];
        existing.LineNumber.Should().Be(0);
        existing.DocumentAbsoluteEntry.Should().Be(661);
        existing.Warehouse.Should().Be("Store1");
        existing.LocationCode.Should().BeNull();

        var added = put.ProductionOrderLines[1];
        added.LineNumber.Should().BeNull();
        added.DocumentAbsoluteEntry.Should().BeNull();
        added.Warehouse.Should().Be("Store1");
        added.ProductionOrderIssueType.Should().Be("im_Manual");
        added.LocationCode.Should().BeNull();
    }

    [Test]
    public async Task PatchProductionOrderLine_SendsPatchWithSinglePreparedLine()
    {
        SapProductionOrderLinePatchRequest? patched = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrderLinePatchRequest, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrderLinePatchRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrderLinePatchRequest body, CancellationToken _) => patched = body)
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

        await _sut.PatchProductionOrderLineAsync(646, newLine, new DateTime(2026, 6, 16));

        patched.Should().NotBeNull();
        patched!.PostingDate.Should().BeNull();
        patched.ProductionOrderLines.Should().ContainSingle();
        var line = patched.ProductionOrderLines!.Single();
        line.ItemNo.Should().Be("RM-200");
        line.LineNumber.Should().BeNull();
        line.VisualOrder.Should().BeNull();
        line.DocumentAbsoluteEntry.Should().BeNull();
        line.ProductionOrderIssueType.Should().Be("im_Manual");
        line.UoMCode.Should().NotBe("KG");
        line.SerialNumbers.Should().BeNull();
        line.BatchNumbers.Should().BeNull();

        var json = JsonSerializer.Serialize(patched);
        json.Should().Contain("\"ProductionOrderLines\"");
        json.Should().NotContain("\"LineNumber\"");
        json.Should().NotContain("\"PostingDate\"");
        json.Should().NotContain("\"PlannedQuantity\":0");
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
        order.ParentProductionOrderNo = null;

        await _sut.CreateProductionOrderAsync(order);

        var json = JsonSerializer.Serialize(posted);
        json.Should().NotContain("U_ProdType");
        json.Should().NotContain("U_DwgNo");
        json.Should().NotContain("U_DocNum");
    }

    [Test]
    public async Task CreateSubassembly_PersistsVirtuallyWithoutPostingToSap()
    {
        await SeedParentAsync();

        var created = await _sut.CreateProductionOrderAsync(BuildSubassembly());

        created!.AbsoluteEntry.Should().Be(-1);
        created.DocumentNumber.Should().Be(10);
        created.ParentProductionOrderNo.Should().Be("10/1");
        created.ParentAbsoluteEntry.Should().Be(646);
        created.ProductionOrderLines.Should().BeNullOrEmpty();

        _http.Verify(
            h => h.PostAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _http.Verify(
            h => h.PutAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        var stored = await _context.ProductionOrders.AsNoTracking()
            .SingleAsync(x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == -1);
        stored.IsVirtualSubassembly.Should().BeTrue();
        stored.ParentAbsoluteEntry.Should().Be(646);
    }

    [Test]
    public async Task CreateParentWithSubassemblies_PostsAllTaggedLinesInOneSapCreate()
    {
        SapProductionOrdersResponse? posted = null;
        SapProductionOrdersResponse? put = null;
        string? postedTag = null;
        double? postedBaseQuantity = null;
        _http.Setup(h => h.PostAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, CancellationToken _) =>
            {
                posted = body;
                postedTag = body.ProductionOrderLines?.SingleOrDefault()?.DocNum;
                postedBaseQuantity = body.ProductionOrderLines?.SingleOrDefault()?.BaseQuantity;
            })
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 671, DocumentNumber = 35 });
        _http.Setup(h => h.GetOrThrowAsync<SapProductionOrdersResponse>(
                It.Is<string>(url => url.Contains("ProductionOrders(671)")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapProductionOrdersResponse
            {
                AbsoluteEntry = 671,
                DocumentNumber = 35,
                ItemNumber = "FG-001",
                Status = "boposPlanned",
                Type = "bopotSpecial",
                Warehouse = "Subcon",
                PlannedQuantity = 5,
                PostingDate = new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc),
                ProductionOrderLines =
                [
                    new SapProductionOrderLines
                    {
                        LineNumber = 0,
                        ItemNo = "CHANNEL-200",
                        PlannedQuantity = 2,
                        Warehouse = "Store1",
                        DocNum = "0-1",
                    },
                ],
            });
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 671, DocumentNumber = 35 });

        var order = BuildOrder();
        order.ProductionOrderLines = [];
        order.Subassemblies =
        [
            new SapProductionOrdersResponse
            {
                DrawingNo = "UAT-DWG-1",
                ProductDescription = "Spool A",
                ProductionOrderLines =
                [
                    new SapProductionOrderLines
                    {
                        ItemNo = "CHANNEL-200",
                        PlannedQuantity = 2,
                        Warehouse = "Store1",
                        ProductionOrderIssueType = "im_Manual",
                    },
                ],
            },
        ];

        var created = await _sut.CreateProductionOrderAsync(order);

        created!.AbsoluteEntry.Should().Be(671);
        posted.Should().NotBeNull();
        posted!.Subassemblies.Should().BeNull();
        posted.DrawingNo.Should().Be("UAT-DWG-1");
        posted.ProductionOrderLines.Should().ContainSingle();
        posted.ProductionOrderLines!.Single().ItemNo.Should().Be("CHANNEL-200");
        posted.ProductionOrderLines.Single().PlannedQuantity.Should().Be(2);
        postedTag.Should().Be("0-1");
        posted.ProductionOrderLines.Single().DrawingNo.Should().Be("UAT-DWG-1");
        posted.ProductionOrderLines.Single().DrawingName.Should().Be("Spool A");
        posted.ProductionOrderLines.Single().FreeText.Should().BeNull();
        postedBaseQuantity.Should().BeNull();
        var json = JsonSerializer.Serialize(posted);
        json.Should().NotContain("Subassemblies");
        json.Should().Contain("\"U_DwgNo\":\"UAT-DWG-1\"");
        json.Should().Contain("\"U_DwgName\":\"Spool A\"");
        json.Should().Contain("\"U_PrjName\":\"Refinery upgrade\"");

        put.Should().NotBeNull();
        put!.ProductionOrderLines.Should().ContainSingle();
        put.ProductionOrderLines!.Single().DocNum.Should().Be("35-1");
        put.ProductionOrderLines.Single().LineNumber.Should().Be(0);
        put.ProductionOrderLines.Single().BaseQuantity.Should().Be(2);

        _http.Verify(
            h => h.PostAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _http.Verify(
            h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.Is<string>(url => url.Contains("ProductionOrders(671)")),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var child = await _context.ProductionOrders.AsNoTracking()
            .SingleAsync(x => x.CompanyDb == CompanyDb && x.IsVirtualSubassembly);
        child.ParentAbsoluteEntry.Should().Be(671);
        child.ParentProductionOrderNo.Should().Be("35/1");
        child.DrawingNo.Should().Be("UAT-DWG-1");
    }

    [Test]
    public async Task CreateParentWithSubassemblies_GetsDocumentNumberWhenCreateOmitsIt()
    {
        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PostAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 671 });
        _http.Setup(h => h.GetOrThrowAsync<SapProductionOrdersResponse>(
                It.Is<string>(url => url.Contains("ProductionOrders(671)")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 671, DocumentNumber = 35 });
        _http.Setup(h => h.GetOrThrowAsync<SapProductionOrdersResponse>(
                It.Is<string>(url => url.Contains("ProductionOrders(671)")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapProductionOrdersResponse
            {
                AbsoluteEntry = 671,
                DocumentNumber = 35,
                ItemNumber = "FG-001",
                Status = "boposPlanned",
                Type = "bopotSpecial",
                Warehouse = "Subcon",
                PlannedQuantity = 5,
                ProductionOrderLines =
                [
                    new SapProductionOrderLines
                    {
                        LineNumber = 0,
                        ItemNo = "CHANNEL-200",
                        PlannedQuantity = 2,
                        Warehouse = "Store1",
                        DocNum = "0-1",
                    },
                ],
            });
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 671, DocumentNumber = 35 });

        var order = BuildOrder();
        order.ProductionOrderLines = [];
        order.Subassemblies =
        [
            new SapProductionOrdersResponse
            {
                DrawingNo = "UAT-DWG-1",
                ProductDescription = "Spool A",
                ProductionOrderLines =
                [
                    new SapProductionOrderLines
                    {
                        ItemNo = "CHANNEL-200",
                        PlannedQuantity = 2,
                        Warehouse = "Store1",
                    },
                ],
            },
        ];

        await _sut.CreateProductionOrderAsync(order);

        put.Should().NotBeNull();
        put!.ProductionOrderLines.Should().ContainSingle(l =>
            l.DocNum == "35-1" && l.BaseQuantity == 2 && l.LineNumber == 0);

        var child = await _context.ProductionOrders.AsNoTracking()
            .SingleAsync(x => x.CompanyDb == CompanyDb && x.IsVirtualSubassembly);
        child.ParentProductionOrderNo.Should().Be("35/1");
    }

    [Test]
    public async Task UpdateSubassemblyItems_RetagsDraftZeroParentToAssignedDocumentNumber()
    {
        await SeedParentAsync();
        await SeedVirtualChildAsync();

        var childRow = await _context.ProductionOrders
            .AsTracking()
            .SingleAsync(x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == -1);
        childRow.ParentProductionOrderNo = "0/1";
        var parentRow = await _context.ProductionOrders
            .AsTracking()
            .Include(x => x.Lines)
            .SingleAsync(x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == 646);
        parentRow.Lines.Add(new ProductionOrderLine
        {
            LineNumber = 2,
            ItemNo = "CHANNEL-200",
            PlannedQuantity = 24,
            Warehouse = "Store1",
            DocNum = "0-1",
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 646, DocumentNumber = 10 });

        var child = BuildSubassembly();
        child.AbsoluteEntry = -1;
        child.ParentProductionOrderNo = "0/1";
        child.DrawingNo = "STG-DWG-1";
        child.ProductDescription = "Staging draft spool";
        child.ProductionOrderLines =
        [
            new SapProductionOrderLines
            {
                ItemNo = "CHANNEL-200",
                PlannedQuantity = 24,
                Warehouse = "Store1",
            },
        ];

        await _sut.UpdateProductionOrderAsync(child);

        put.Should().NotBeNull();
        put!.ProductionOrderLines.Should().ContainSingle(l =>
            l.ItemNo == "CHANNEL-200" && l.DocNum == "10-1" && l.BaseQuantity == 24);
        put.ProductionOrderLines.Should().NotContain(l => l.DocNum == "0-1");

        var stored = await _context.ProductionOrders.AsNoTracking()
            .SingleAsync(x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == -1);
        stored.ParentProductionOrderNo.Should().Be("10/1");
    }

    [Test]
    public async Task CreateSubassembly_WithItems_PutsMergedParentOnce()
    {
        await SeedParentAsync();

        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 646, DocumentNumber = 10 });

        var child = BuildSubassembly();
        child.ProductionOrderLines =
        [
            new SapProductionOrderLines
            {
                ItemNo = "CHANNEL-200",
                PlannedQuantity = 24,
                Warehouse = "Store1",
                ProductionOrderIssueType = "im_Manual",
            },
        ];

        var created = await _sut.CreateProductionOrderAsync(child);

        created!.AbsoluteEntry.Should().Be(-1);
        put.Should().NotBeNull();
        put!.ProductionOrderLines.Should().ContainSingle(l => l.ItemNo == "CHANNEL-200" && l.DocNum == "10-1");

        _http.Verify(
            h => h.PostAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _http.Verify(
            h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.Is<string>(url => url.Contains("ProductionOrders(646)")),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task UpdateSubassemblyItems_PutsMergedParentIncludingBom()
    {
        await SeedParentAsync();
        await SeedVirtualChildAsync();

        var parentRow = await _context.ProductionOrders
            .AsTracking()
            .Include(x => x.Lines)
            .SingleAsync(x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == 646);
        parentRow.Lines.Single(x => x.ItemNo == "RM-BOM").LocationCode = 7;
        parentRow.Lines.Add(new ProductionOrderLine
        {
            LineNumber = 6,
            ItemNo = "RM-WIP",
            PlannedQuantity = 1,
            Warehouse = "WIP",
            LocationCode = 2,
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 646, DocumentNumber = 10 });

        var child = BuildSubassembly();
        child.AbsoluteEntry = -1;
        child.ProductionOrderLines =
        [
            new SapProductionOrderLines
            {
                ItemNo = "CHANNEL-200",
                PlannedQuantity = 24,
                Warehouse = "Store1",
                ProductionOrderIssueType = "im_Manual",
            },
        ];

        await _sut.UpdateProductionOrderAsync(child);

        put.Should().NotBeNull();
        put!.AbsoluteEntry.Should().Be(646);
        put.ParentProductionOrderNo.Should().BeNull();
        put.ProductionOrderLines.Should().HaveCount(3);
        put.ProductionOrderLines.Should().ContainSingle(l => l.ItemNo == "RM-BOM");
        put.ProductionOrderLines.Should().ContainSingle(l => l.ItemNo == "RM-WIP");
        put.ProductionOrderLines!.Should().OnlyContain(l => l.LineNumber == null && l.DocumentAbsoluteEntry == null);

        var tagged = put.ProductionOrderLines!.Single(l => l.ItemNo == "CHANNEL-200");
        tagged.DocNum.Should().Be("10-1");
        tagged.Warehouse.Should().Be("Store1");
        tagged.LocationCode.Should().Be(7);
        tagged.ItemType.Should().Be("pit_Item");
        tagged.ProductionOrderIssueType.Should().Be("im_Manual");
        tagged.Project.Should().Be("PRJ-1");
        tagged.FreeText.Should().BeNull();

        _http.Verify(
            h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.Is<string>(url => url.Contains("ProductionOrders(646)")),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _http.Verify(
            h => h.PatchAsync<SapProductionOrderLinePatchRequest, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrderLinePatchRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _http.Verify(
            h => h.PostAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task UpdateSubassemblyItems_PutsWhenReplacingExistingTaggedLines()
    {
        await SeedParentAsync();
        await SeedVirtualChildAsync();

        var parentRow = await _context.ProductionOrders
            .AsTracking()
            .Include(x => x.Lines)
            .SingleAsync(x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == 646);
        parentRow.Lines.Add(new ProductionOrderLine
        {
            LineNumber = 2,
            ItemNo = "OLD-SA",
            PlannedQuantity = 1,
            Warehouse = "Store1",
            LocationCode = 9,
            DocNum = "10/1",
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 646, DocumentNumber = 10 });

        var child = BuildSubassembly();
        child.AbsoluteEntry = -1;
        child.DrawingNo = "PBBPL-A-1234-5";
        child.ProductDescription = "Test Drawing No.5";
        child.ProductionOrderLines =
        [
            new SapProductionOrderLines
            {
                ItemNo = "CHANNEL-200",
                PlannedQuantity = 24,
                Warehouse = "Store1",
                ProductionOrderIssueType = "im_Manual",
            },
        ];

        await _sut.UpdateProductionOrderAsync(child);

        put.Should().NotBeNull();
        put!.ProductionOrderLines.Should().ContainSingle(l => l.ItemNo == "RM-BOM");
        put.ProductionOrderLines.Should().ContainSingle(l => l.ItemNo == "CHANNEL-200" && l.DocNum == "10-1");
        put.ProductionOrderLines.Should().NotContain(l => l.ItemNo == "OLD-SA");
        put.ProductionOrderLines!.Should().OnlyContain(l => l.LineNumber == null && l.DocumentAbsoluteEntry == null);
        put.DrawingNo.Should().Be("PBBPL-A-1234-5");
        put.ProductionOrderLines.Single(l => l.ItemNo == "CHANNEL-200").LocationCode.Should().Be(9);
        put.ProductionOrderLines.Single(l => l.ItemNo == "CHANNEL-200").FreeText.Should().BeNull();
        put.ProductionOrderLines.Single(l => l.ItemNo == "CHANNEL-200").DrawingName.Should().Be("Test Drawing No.5");
        put.ProductionOrderLines.Single(l => l.ItemNo == "CHANNEL-200").DrawingNo.Should().Be("PBBPL-A-1234-5");
        put.ProductionOrderLines.Single(l => l.ItemNo == "CHANNEL-200").LineText.Should().BeNull();
        put.ProductionOrderLines!.Should().OnlyContain(l => l.IssuedQuantity == null && l.ItemName == null && l.LineText == null);

        _http.Verify(
            h => h.PatchAsync<SapProductionOrderLinePatchRequest, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrderLinePatchRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task UpdateSubassemblyItems_AllowsTheSameItemOnAnotherSubassembly()
    {
        await SeedParentAsync();
        await SeedVirtualChildAsync();

        var parentRow = await _context.ProductionOrders
            .AsTracking()
            .Include(x => x.Lines)
            .SingleAsync(x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == 646);
        parentRow.Lines.Add(new ProductionOrderLine
        {
            LineNumber = 2,
            ItemNo = "RM5708606200380",
            PlannedQuantity = 12,
            Warehouse = "Store1",
            LocationCode = 2,
            DocNum = "10-2",
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 646, DocumentNumber = 10 });

        var child = BuildSubassembly();
        child.AbsoluteEntry = -1;
        child.ProductionOrderLines =
        [
            new SapProductionOrderLines
            {
                ItemNo = "RM5708606200380",
                PlannedQuantity = 4,
                Warehouse = "Store1",
            },
        ];

        await _sut.UpdateProductionOrderAsync(child);

        put.Should().NotBeNull();
        put!.ProductionOrderLines.Should().ContainSingle(l =>
            l.ItemNo == "RM5708606200380" && l.DocNum == "10-2" && l.PlannedQuantity == 12);
        put.ProductionOrderLines.Should().ContainSingle(l =>
            l.ItemNo == "RM5708606200380" && l.DocNum == "10-1" && l.PlannedQuantity == 4);
        put.ProductionOrderLines.Should().ContainSingle(l => l.ItemNo == "RM-BOM");
        put.ProductionOrderLines.Single(l => l.DocNum == "10-1").LocationCode.Should().Be(2);
    }

    [Test]
    public async Task UpdateSubassemblyItems_KeepsLineNumberWhenAComponentIsIssued()
    {
        await SeedParentAsync();
        await SeedVirtualChildAsync();

        var parentRow = await _context.ProductionOrders
            .AsTracking()
            .Include(x => x.Lines)
            .SingleAsync(x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == 646);
        parentRow.Lines.Single(x => x.ItemNo == "RM-BOM").IssuedQuantity = 12;
        parentRow.Lines.Add(new ProductionOrderLine
        {
            LineNumber = 2,
            ItemNo = "CHANNEL-200",
            PlannedQuantity = 24,
            IssuedQuantity = 6,
            Warehouse = "Store1",
            DocNum = "10/1",
            DrawingNo = "OLD-DWG",
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 646, DocumentNumber = 10 });

        var child = BuildSubassembly();
        child.AbsoluteEntry = -1;
        child.DrawingNo = "PBBPL-A-1234-5";
        child.ProductDescription = "Test Drawing No.5";
        child.ProductionOrderLines =
        [
            new SapProductionOrderLines
            {
                ItemNo = "CHANNEL-200",
                PlannedQuantity = 24,
                Warehouse = "Store1",
                ProductionOrderIssueType = "im_Manual",
                FreeText = "cut extra",
            },
        ];

        await _sut.UpdateProductionOrderAsync(child);

        put.Should().NotBeNull();
        put!.ProductionOrderLines.Should().ContainSingle(l => l.ItemNo == "RM-BOM" && l.LineNumber == 0);
        var tagged = put.ProductionOrderLines!.Single(l => l.ItemNo == "CHANNEL-200");
        tagged.LineNumber.Should().Be(2);
        tagged.DocNum.Should().Be("10-1");
        tagged.DrawingNo.Should().Be("PBBPL-A-1234-5");
        tagged.DrawingName.Should().Be("Test Drawing No.5");
        tagged.FreeText.Should().Be("cut extra");
        tagged.IssuedQuantity.Should().BeNull();
        tagged.BaseQuantity.Should().Be(24);
        put.ProductionOrderLines.Single(l => l.ItemNo == "RM-BOM").BaseQuantity.Should().Be(12);
    }

    [Test]
    public async Task UpdateParent_KeepsTaggedSubassemblyLines()
    {
        var now = DateTime.UtcNow;
        _context.ProductionOrders.Add(new ProductionOrder
        {
            CompanyDb = CompanyDb,
            AbsoluteEntry = 646,
            DocumentNumber = 10,
            Warehouse = "WIP",
            SyncedAtUtc = now,
            CreatedOn = now,
            LastModifiedOn = now,
            Lines =
            [
                new ProductionOrderLine { LineNumber = 0, ItemNo = "RM-BOM", Warehouse = "Store1" },
                new ProductionOrderLine { LineNumber = 2, ItemNo = "CHANNEL-200", Warehouse = "Store1", DocNum = "10/1" },
            ],
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 646, DocumentNumber = 10 });

        var parent = BuildOrder();
        parent.AbsoluteEntry = 646;
        parent.Warehouse = "WIP";
        parent.ProductionOrderLines =
        [
            new SapProductionOrderLines
            {
                LineNumber = 0,
                ItemNo = "RM-BOM",
                PlannedQuantity = 12,
                Warehouse = "Store1",
            },
        ];

        await _sut.UpdateProductionOrderAsync(parent);

        put!.ProductionOrderLines.Should().HaveCount(2);
        put.ProductionOrderLines!.Select(l => l.ItemNo).Should().Equal("RM-BOM", "CHANNEL-200");
        put.ProductionOrderLines.Single(l => l.ItemNo == "CHANNEL-200").DocNum.Should().Be("10-1");
        put.ProductionOrderLines.Single(l => l.ItemNo == "CHANNEL-200").LineNumber.Should().Be(2);
    }

    [Test]
    public async Task GetVirtualSubassembly_NeverCallsSap()
    {
        await SeedVirtualChildAsync();

        var loaded = await _sut.GetProductionOrders("-1");

        loaded.Should().NotBeNull();
        loaded!.AbsoluteEntry.Should().Be(-1);
        loaded.ParentProductionOrderNo.Should().Be("10/1");
        _http.VerifyNoOtherCalls();
    }

    [Test]
    public async Task CancelSubassembly_RemovesTaggedLinesFromParent()
    {
        var now = DateTime.UtcNow;
        _context.ProductionOrders.Add(new ProductionOrder
        {
            CompanyDb = CompanyDb,
            AbsoluteEntry = 646,
            DocumentNumber = 10,
            Warehouse = "WIP",
            Status = "boposPlanned",
            SyncedAtUtc = now,
            CreatedOn = now,
            LastModifiedOn = now,
            Lines =
            [
                new ProductionOrderLine { LineNumber = 0, ItemNo = "RM-BOM", Warehouse = "Store1" },
                new ProductionOrderLine { LineNumber = 2, ItemNo = "CHANNEL-200", Warehouse = "Store1", DocNum = "10/1" },
            ],
        });
        _context.ProductionOrders.Add(new ProductionOrder
        {
            CompanyDb = CompanyDb,
            AbsoluteEntry = -1,
            DocumentNumber = 10,
            Warehouse = "WIP",
            Status = "boposPlanned",
            IsVirtualSubassembly = true,
            ParentAbsoluteEntry = 646,
            ParentProductionOrderNo = "10/1",
            SyncedAtUtc = now,
            CreatedOn = now,
            LastModifiedOn = now,
            Lines =
            [
                new ProductionOrderLine { LineNumber = 0, ItemNo = "CHANNEL-200", Warehouse = "Store1", DocNum = "10/1" },
            ],
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        SapProductionOrdersResponse? put = null;
        _http.Setup(h => h.PatchAsync<SapProductionOrdersResponse, SapProductionOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapProductionOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, SapProductionOrdersResponse body, IReadOnlyDictionary<string, string> _, CancellationToken _) => put = body)
            .ReturnsAsync(new SapProductionOrdersResponse { AbsoluteEntry = 646, DocumentNumber = 10 });

        await _sut.CancelProductionOrderAsync(-1);

        put.Should().NotBeNull();
        put!.AbsoluteEntry.Should().Be(646);
        put.ProductionOrderLines.Should().ContainSingle(l => l.ItemNo == "RM-BOM");
        put.ProductionOrderLines.Should().NotContain(l => l.DocNum == "10/1" || l.DocNum == "10-1");

        var child = await _context.ProductionOrders.AsNoTracking()
            .SingleAsync(x => x.CompanyDb == CompanyDb && x.AbsoluteEntry == -1);
        child.Status.Should().Be("boposCancelled");
    }

    private static SapProductionOrdersResponse BuildSubassembly() => new()
    {
        ItemNumber = "FG-001",
        Status = "boposPlanned",
        Type = "bopotSpecial",
        ProductionCategory = "JOB",
        ParentProductionOrderNo = "10/1",
        ParentAbsoluteEntry = 646,
        Warehouse = "WIP",
        PlannedQuantity = 5,
        ProductionOrderLines = [],
    };

    private async Task SeedParentAsync()
    {
        var now = DateTime.UtcNow;
        _context.ProductionOrders.Add(new ProductionOrder
        {
            CompanyDb = CompanyDb,
            AbsoluteEntry = 646,
            DocumentNumber = 10,
            Warehouse = "WIP",
            ItemNo = "FG-001",
            Project = "PRJ-1",
            ProjectName = "Refinery upgrade",
            Status = "boposPlanned",
            SyncedAtUtc = now,
            CreatedOn = now,
            LastModifiedOn = now,
            Lines =
            [
                new ProductionOrderLine
                {
                    LineNumber = 0,
                    ItemNo = "RM-BOM",
                    PlannedQuantity = 12,
                    Warehouse = "Store1",
                },
            ],
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private async Task SeedVirtualChildAsync()
    {
        var now = DateTime.UtcNow;
        _context.ProductionOrders.Add(new ProductionOrder
        {
            CompanyDb = CompanyDb,
            AbsoluteEntry = -1,
            DocumentNumber = 10,
            Warehouse = "WIP",
            ItemNo = "FG-001",
            Status = "boposPlanned",
            IsVirtualSubassembly = true,
            ParentAbsoluteEntry = 646,
            ParentProductionOrderNo = "10/1",
            SyncedAtUtc = now,
            CreatedOn = now,
            LastModifiedOn = now,
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }
}
