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
using SapApi.Infrastructure.Services.PurchaseRequests;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Tests.Services.PurchaseOrders;
using SapApi.Tests.Services.PurchaseRequests;

namespace SapApi.Tests.Services.Sap;

[TestFixture]
public class SapPurchaseRequestServiceReportTests
{
    private const string CompanyDb = "PBBPL_UAT";
    private const int RequesterId = 1;

    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private Mock<IHttpRequestHandler> _http = null!;
    private SapPurchaseRequestService _sut = null!;

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

        var localStore = PurchaseRequestLocalStoreTestHelper.Create(
            _context, _http.Object, companyDbAccessor.Object);
        var purchaseOrderLinks = new PurchaseOrderLinkResolver(
            _context, companyDbAccessor.Object,
            PurchaseOrderLocalStoreTestHelper.Create(_context, _http.Object, companyDbAccessor.Object));
        var approvalService = new ApprovalService(
            _context, httpContextAccessor, companyDbAccessor.Object, purchaseOrderLinks);

        _sut = new SapPurchaseRequestService(
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

    private static SapPurchaseRequestsResponse Document(int docEntry, int docNum) => new()
    {
        DocEntry = docEntry,
        DocNum = docNum,
        DocType = Constants.PurchaseOrderDocType.Document_Item,
        Requester = "manager",
        RequesterName = "Laxman Gawali",
        Project = "PB/R&M/001",
        DocDate = new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc),
        DocumentLines =
        [
            new()
            {
                LineNum = 0,
                ItemCode = "RM1",
                ItemDescription = "Beam",
                Quantity = 10,
                UoMCode = "KG",
                RequiredDate = new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc),
            },
        ],
    };

    [Test]
    public async Task Pulls_rows_live_from_sap_not_the_local_mirror()
    {
        // Seed a local-mirror row that must NOT appear in the results — the report reads SAP, not Postgres.
        _context.PurchaseRequests.Add(new PurchaseRequest
        {
            CompanyDb = CompanyDb,
            DocEntry = 999,
            DocNum = 999,
            Project = "STALE",
            DocDate = DateTime.UtcNow,
            Lines = [new PurchaseRequestLine { LineNum = 0, ItemCode = "STALE-ITEM", Quantity = 1 }],
        });
        await _context.SaveChangesAsync();

        _http
            .Setup(h => h.GetAsync<GetAllSapPurchaseRequestsResponse>(
                It.Is<string>(url => url.Contains("PurchaseRequests", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAllSapPurchaseRequestsResponse { Value = [Document(1, 101)] });

        var rows = await _sut.GetReportRowsFromSapAsync([], CancellationToken.None);

        rows.Should().ContainSingle();
        rows[0].DocNum.Should().Be(101);
        rows[0].ItemCode.Should().Be("RM1");
        rows.Should().NotContain(r => r.ItemCode == "STALE-ITEM");
    }

    [Test]
    public async Task Maps_service_docs_username_and_line_level_overrides()
    {
        var serviceDoc = Document(2, 202) with
        {
            DocType = Constants.PurchaseOrderDocType.Document_Service,
            RequesterName = null,
            DocumentLines =
            [
                new()
                {
                    LineNum = 0,
                    ItemCode = "SVC-01",
                    ItemDescription = "Transport",
                    Quantity = 1,
                    UoMCode = "NOS",
                    FreeText = "One-time pickup",
                    ProjectCode = "PB/OVERRIDE",
                },
            ],
        };

        _http
            .Setup(h => h.GetAsync<GetAllSapPurchaseRequestsResponse>(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAllSapPurchaseRequestsResponse { Value = [serviceDoc] });

        var rows = await _sut.GetReportRowsFromSapAsync([], CancellationToken.None);

        rows.Should().ContainSingle();
        rows[0].ItemType.Should().Be("Service");
        rows[0].UserName.Should().Be("manager"); // RequesterName blank -> falls back to Requester code
        rows[0].ProjectCode.Should().Be("PB/OVERRIDE"); // line-level project overrides header
        rows[0].FreeText.Should().Be("One-time pickup");
    }

    [Test]
    public async Task Sends_a_sap_filter_covering_every_dialog_field()
    {
        string? capturedUrl = null;
        _http
            .Setup(h => h.GetAsync<GetAllSapPurchaseRequestsResponse>(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, bool, bool, CancellationToken>((url, _, _, _) => capturedUrl ??= url)
            .ReturnsAsync(new GetAllSapPurchaseRequestsResponse { Value = [] });

        var filters = new List<FilterModel>
        {
            new() { Field = "Username", Operator = "contains", Value = "manager" },
            new() { Field = "DocDate", Operator = "gte", Value = "2026-08-01" },
            new() { Field = "DocDate", Operator = "lte", Value = "2026-08-31" },
            new() { Field = "RequiredDate", Operator = "gte", Value = "2026-08-05" },
            new() { Field = "Project", Operator = "gte", Value = "AAA" },
            new() { Field = "BPLId", Operator = "eq", Value = "1" },
        };

        await _sut.GetReportRowsFromSapAsync(filters, CancellationToken.None);

        capturedUrl.Should().NotBeNull();
        var decoded = Uri.UnescapeDataString(capturedUrl!);
        decoded.Should().Contain("contains(Requester,'manager')");
        decoded.Should().Contain("DocDate ge '2026-08-01'");
        decoded.Should().Contain("DocDate le '2026-08-31'");
        decoded.Should().Contain("RequriedDate ge '2026-08-05'");
        decoded.Should().Contain("Project ge 'AAA'");
        decoded.Should().Contain("BPLId eq 1");
    }

    [Test]
    public async Task Period_filter_translates_to_a_docdate_month_window()
    {
        string? capturedUrl = null;
        _http
            .Setup(h => h.GetAsync<GetAllSapPurchaseRequestsResponse>(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, bool, bool, CancellationToken>((url, _, _, _) => capturedUrl ??= url)
            .ReturnsAsync(new GetAllSapPurchaseRequestsResponse { Value = [] });

        var filters = new List<FilterModel> { new() { Field = "Period", Operator = "eq", Value = "2026-08" } };

        await _sut.GetReportRowsFromSapAsync(filters, CancellationToken.None);

        var decoded = Uri.UnescapeDataString(capturedUrl!);
        decoded.Should().Contain("DocDate ge '2026-08-01'");
        decoded.Should().Contain("DocDate lt '2026-09-01'");
    }

    [Test]
    public async Task Pages_through_sap_until_a_short_page_is_returned()
    {
        var pageCalls = 0;
        _http
            .Setup(h => h.GetAsync<GetAllSapPurchaseRequestsResponse>(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                pageCalls++;
                return pageCalls == 1
                    ? new GetAllSapPurchaseRequestsResponse
                    {
                        Value = Enumerable.Range(1, 100).Select(i => Document(i, i)).ToList(),
                    }
                    : new GetAllSapPurchaseRequestsResponse { Value = [Document(101, 101)] };
            });

        var rows = await _sut.GetReportRowsFromSapAsync([], CancellationToken.None);

        pageCalls.Should().Be(2);
        rows.Should().HaveCount(101);
    }
}
