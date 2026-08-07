using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services.PurchaseOrders;

[TestFixture]
public class PurchaseOrderListFilterTests
{
    private const string CompanyDb = "PBBPL_UAT";

    private AppDbContext _context = null!;
    private Mock<IHttpRequestHandler> _http = null!;
    private PurchaseOrderLocalStore _sut = null!;

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

        _sut = PurchaseOrderLocalStoreTestHelper.Create(_context, _http.Object, companyDb.Object);
        SeedPurchaseOrders();
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public async Task ListFromDb_ProjectFilter_MatchesMiddleOfProjectName()
    {
        _http.Setup(h => h.GetAsync<SapGetAllProjectDetailsResponse>(
                It.Is<string>(url => url.Contains("Projects", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapGetAllProjectDetailsResponse
            {
                Value =
                [
                    new SapProjectDetailsResponse { ProjectCode = "PRJ-100", ProjectName = "Alpha Renovation" },
                ],
            });

        var response = await _sut.ListFromDbAsync(new PaginationRequest
        {
            PageNumber = 1,
            PageSize = 20,
            Filters =
            [
                new FilterModel { Field = "Project", Operator = "contains", Value = "Renov" },
            ],
        });

        response.Data.Should().ContainSingle();
        response.Data![0].Project.Should().Be("PRJ-100");
    }

    [Test]
    public async Task ListFromDb_CardCodeFilter_MatchesMiddleOfCardName()
    {
        var response = await _sut.ListFromDbAsync(new PaginationRequest
        {
            PageNumber = 1,
            PageSize = 20,
            Filters =
            [
                new FilterModel { Field = "CardCode", Operator = "contains", Value = "indust" },
            ],
        });

        response.Data.Should().ContainSingle();
        response.Data![0].CardCode.Should().Be("V001");
    }

    [Test]
    public async Task ListFromDb_BranchFilter_MatchesBranchName()
    {
        _http.Setup(h => h.GetAsync<SapGetAllBranchesResponse>(
                It.Is<string>(url => url.Contains("BusinessPlaces", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapGetAllBranchesResponse
            {
                Value =
                [
                    new SapBranchesResponse { BplId = 2, BplName = "Mumbai Branch" },
                ],
            });

        var response = await _sut.ListFromDbAsync(new PaginationRequest
        {
            PageNumber = 1,
            PageSize = 20,
            Filters =
            [
                new FilterModel { Field = "BPLId", Operator = "contains", Value = "Mumbai" },
            ],
        });

        response.Data.Should().ContainSingle();
        response.Data![0].BPLId.Should().Be(2);
        response.Data![0].BPLIdClient.Should().Be(2);
    }

    private void SeedPurchaseOrders()
    {
        _context.PurchaseOrders.AddRange(
            new PurchaseOrder
            {
                CompanyDb = CompanyDb,
                DocEntry = 1,
                DocNum = 100,
                Project = "PRJ-100",
                CardCode = "V001",
                CardName = "Acme Industries",
                BPLId = 2,
                DocDate = new DateTime(2026, 2, 10),
                CreatedOn = DateTime.UtcNow,
                LastModifiedOn = DateTime.UtcNow,
                SyncedAtUtc = DateTime.UtcNow,
            },
            new PurchaseOrder
            {
                CompanyDb = CompanyDb,
                DocEntry = 2,
                DocNum = 101,
                Project = "PRJ-200",
                CardCode = "V002",
                CardName = "Other Vendor",
                BPLId = 1,
                DocDate = new DateTime(2026, 2, 11),
                CreatedOn = DateTime.UtcNow,
                LastModifiedOn = DateTime.UtcNow,
                SyncedAtUtc = DateTime.UtcNow,
            });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }
}
