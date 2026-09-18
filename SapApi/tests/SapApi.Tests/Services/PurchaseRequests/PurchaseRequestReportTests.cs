using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services.PurchaseRequests;
using SapApi.Shared;
using SapApi.Shared.Models;

namespace SapApi.Tests.Services.PurchaseRequests;

[TestFixture]
public class PurchaseRequestReportTests
{
    private const string CompanyDb = "PBBPL_UAT";

    private AppDbContext _context = null!;
    private PurchaseRequestLocalStore _sut = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        var http = new Mock<IHttpRequestHandler>();
        var companyDbAccessor = new Mock<ICurrentCompanyDbAccessor>();
        companyDbAccessor.Setup(x => x.GetCompanyDbName()).Returns(CompanyDb);

        _sut = PurchaseRequestLocalStoreTestHelper.Create(_context, http.Object, companyDbAccessor.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private async Task SeedAsync(PurchaseRequest request)
    {
        _context.PurchaseRequests.Add(request);
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task ListReportLinesAsync_flattens_lines_across_documents_in_doc_date_order()
    {
        await SeedAsync(new PurchaseRequest
        {
            CompanyDb = CompanyDb,
            DocEntry = 2,
            DocNum = 202,
            DocType = Constants.PurchaseOrderDocType.Document_Item,
            RequesterName = "Laxman Gawali",
            Project = "CO6920",
            DocDate = new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc),
            Lines =
            [
                new PurchaseRequestLine
                {
                    LineNum = 0,
                    ItemCode = "CO6920400011100266",
                    ItemDescription = "FILLER WIRE 2.4 MM THK 80S B2 ADOR MAKE",
                    Quantity = 60,
                    UoMCode = "KG",
                    RequiredDate = new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc),
                },
            ],
        });
        await SeedAsync(new PurchaseRequest
        {
            CompanyDb = CompanyDb,
            DocEntry = 1,
            DocNum = 101,
            DocType = Constants.PurchaseOrderDocType.Document_Service,
            RequesterName = "Ayush Aher",
            Project = "CO0001",
            DocDate = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            Lines =
            [
                new PurchaseRequestLine
                {
                    LineNum = 0,
                    ItemCode = "SVC-01",
                    ItemDescription = "Transport",
                    Quantity = 1,
                    UoMCode = "NOS",
                    FreeText = "One-time pickup",
                },
            ],
        });

        var rows = await _sut.ListReportLinesAsync([], CancellationToken.None);

        rows.Should().HaveCount(2);
        rows[0].DocNum.Should().Be(101);
        rows[0].ItemType.Should().Be("Service");
        rows[0].FreeText.Should().Be("One-time pickup");
        rows[1].DocNum.Should().Be(202);
        rows[1].ItemType.Should().Be("Item");
        rows[1].ProjectCode.Should().Be("CO6920");
        rows[1].Quantity.Should().Be(60);
        rows[1].Unit.Should().Be("KG");
    }

    [Test]
    public async Task ListReportLinesAsync_applies_project_range_filter()
    {
        await SeedAsync(new PurchaseRequest
        {
            CompanyDb = CompanyDb,
            DocEntry = 1,
            DocNum = 1,
            Project = "AAA",
            DocDate = DateTime.UtcNow,
            Lines = [new PurchaseRequestLine { LineNum = 0, ItemCode = "X1", Quantity = 1 }],
        });
        await SeedAsync(new PurchaseRequest
        {
            CompanyDb = CompanyDb,
            DocEntry = 2,
            DocNum = 2,
            Project = "ZZZ",
            DocDate = DateTime.UtcNow,
            Lines = [new PurchaseRequestLine { LineNum = 0, ItemCode = "X2", Quantity = 1 }],
        });

        var filters = new List<FilterModel>
        {
            new() { Field = "Project", Operator = "gte", Value = "M" },
        };

        var rows = await _sut.ListReportLinesAsync(filters, CancellationToken.None);

        rows.Should().ContainSingle();
        rows[0].ProjectCode.Should().Be("ZZZ");
    }
}
