using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Caching;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Tests.Services.PurchaseOrders;

namespace SapApi.Tests.Services.Sap;

[TestFixture]
public class SapPurchaseOrderServiceWriteTests
{
    private const string CompanyDb = "PBBPL_UAT";
    private const int RequesterId = 1;

    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private Mock<IHttpRequestHandler> _http = null!;
    private SapPurchaseOrderService _sut = null!;

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

        var sapLogin = new Mock<ISapLoginService>();
        sapLogin.Setup(s => s.SapLoginAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var cache = new SapMasterDataCache(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        var masterData = new SapMasterDataService(_http.Object, sapLogin.Object, cache, companyDbAccessor.Object);

        var localStore = PurchaseOrderLocalStoreTestHelper.Create(
            _context, _http.Object, companyDbAccessor.Object);
        var purchaseOrderLinks = new PurchaseOrderLinkResolver(
            _context, companyDbAccessor.Object, localStore);
        var approvalService = new ApprovalService(
            _context, httpContextAccessor, companyDbAccessor.Object, purchaseOrderLinks);

        _sut = new SapPurchaseOrderService(
            _http.Object,
            approvalService,
            localStore,
            new SapDocumentSeriesService(_http.Object),
            masterData);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task UpdatePurchaseOrder_PatchesWithReplaceCollectionsInsteadOfPut()
    {
        string? url = null;
        IReadOnlyDictionary<string, string>? headers = null;
        _http.Setup(h => h.PatchAsync<SapPurchaseOrdersResponse, SapPurchaseOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapPurchaseOrdersResponse>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback((string u, SapPurchaseOrdersResponse _, IReadOnlyDictionary<string, string> h, CancellationToken _) =>
            {
                url = u;
                headers = h;
            })
            .ReturnsAsync((SapPurchaseOrdersResponse?)null);
        _http.Setup(h => h.GetOrThrowAsync<SapPurchaseOrdersResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapPurchaseOrdersResponse { DocEntry = 4544, CardCode = "S000035" });

        await _sut.UpdatePurchaseOrder(new SapPurchaseOrdersResponse
        {
            DocEntry = 4544,
            CardCode = "S000035",
            Comments = "updated from UI",
            DocumentLines =
            [
                new()
                {
                    LineNum = 0,
                    ItemCode = "RM5703802100003800",
                    Quantity = 1,
                    UnitPrice = 111,
                    WarehouseCode = "PBPL(S)",
                    LocationCode = 2,
                },
            ],
        });

        url.Should().Contain("PurchaseOrders(4544)");
        headers.Should().ContainKey(Constants.SapServiceLayerHeaders.ReplaceCollectionsOnPatch)
            .WhoseValue.Should().Be("true");
        _http.Verify(
            h => h.PutAsync<SapPurchaseOrdersResponse, SapPurchaseOrdersResponse>(
                It.IsAny<string>(),
                It.IsAny<SapPurchaseOrdersResponse>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task GetPurchaseOrders_merges_document_special_lines_from_sap()
    {
        var po = new PurchaseOrder
        {
            CompanyDb = CompanyDb,
            DocEntry = 4549,
            DocNum = 262710081,
            CardCode = "S000035",
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = DateTime.UtcNow,
            SyncedAtUtc = DateTime.UtcNow,
        };
        _context.PurchaseOrders.Add(po);
        await _context.SaveChangesAsync();
        _context.PurchaseOrderLines.Add(new PurchaseOrderLine
        {
            PurchaseOrderId = po.Id,
            LineNum = 0,
            ItemCode = "RM1",
            ItemDescription = "Plate",
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _http.Setup(h => h.GetAsync<SapPurchaseOrdersResponse>(
                It.Is<string>(url => url.Contains("PurchaseOrders(4549)", StringComparison.Ordinal)),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapPurchaseOrdersResponse
            {
                DocEntry = 4549,
                DocumentSpecialLines =
                [
                    new SapDocumentSpecialLine
                    {
                        AfterLineNumber = 0,
                        LineText = "Make as per drawing D-101",
                    },
                ],
            });

        var result = await _sut.GetPurchaseOrders("4549");

        result.Should().NotBeNull();
        result!.DocumentSpecialLines.Should().ContainSingle().Which.LineText.Should().Be("Make as per drawing D-101");
        result.DocumentLines.Should().ContainSingle().Which.FreeText.Should().Be("Make as per drawing D-101");
    }
}
