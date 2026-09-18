using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services.Items;
using SapApi.Shared.Models;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services.Items;

[TestFixture]
public class ItemLocalStoreTests
{
    private const string CompanyDb = "PBBPL_UAT";

    private AppDbContext _context = null!;
    private Mock<IHttpRequestHandler> _http = null!;
    private ItemLocalStore _sut = null!;

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

        var companyDbAccessor = new Mock<ICurrentCompanyDbAccessor>();
        companyDbAccessor.Setup(x => x.GetCompanyDbName()).Returns(CompanyDb);

        _sut = new ItemLocalStore(_context, _http.Object, companyDbAccessor.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public async Task UpsertFromSapAsync_creates_then_updates_the_same_row()
    {
        await _sut.UpsertFromSapAsync(new ItemsResponse { ItemCode = "RM1", ItemName = "Raw Material 1", InventoryUom = "KG" });
        await _sut.UpsertFromSapAsync(new ItemsResponse { ItemCode = "RM1", ItemName = "Raw Material 1 (renamed)", InventoryUom = "NOS" });

        var rows = await _context.Items.Where(x => x.CompanyDb == CompanyDb).ToListAsync();
        rows.Should().ContainSingle();
        rows[0].ItemName.Should().Be("Raw Material 1 (renamed)");
        rows[0].InventoryUom.Should().Be("NOS");
    }

    [Test]
    public async Task ListFromDbAsync_search_matches_item_code_or_name()
    {
        await _sut.UpsertFromSapAsync(new ItemsResponse { ItemCode = "CO6920", ItemName = "Filler Wire" });
        await _sut.UpsertFromSapAsync(new ItemsResponse { ItemCode = "XYZ", ItemName = "Bracket" });

        var byCode = await _sut.ListFromDbAsync(new PaginationRequest
        {
            PageNumber = 1,
            PageSize = 10,
            Filters = [new FilterModel { Field = "__search", Operator = "contains", Value = "6920" }],
        });
        byCode.Data.Should().ContainSingle().Which.ItemCode.Should().Be("CO6920");

        var byName = await _sut.ListFromDbAsync(new PaginationRequest
        {
            PageNumber = 1,
            PageSize = 10,
            Filters = [new FilterModel { Field = "__search", Operator = "contains", Value = "bracket" }],
        });
        byName.Data.Should().ContainSingle().Which.ItemCode.Should().Be("XYZ");
    }

    [Test]
    public async Task ListFromDbAsync_with_empty_group_codes_returns_nothing_instead_of_an_unfiltered_page()
    {
        await _sut.UpsertFromSapAsync(new ItemsResponse { ItemCode = "RM1", ItemsGroupCode = 100 });

        var result = await _sut.ListFromDbAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            groupCodes: []);

        result.Data.Should().BeEmpty();
    }

    [Test]
    public async Task ListFromDbAsync_with_null_group_codes_is_unfiltered()
    {
        await _sut.UpsertFromSapAsync(new ItemsResponse { ItemCode = "RM1", ItemsGroupCode = 100 });

        var result = await _sut.ListFromDbAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            groupCodes: null);

        result.Data.Should().ContainSingle();
    }

    [Test]
    public async Task ListFromDbAsync_filters_by_resolved_group_codes()
    {
        await _sut.UpsertFromSapAsync(new ItemsResponse { ItemCode = "RM1", ItemsGroupCode = 100 });
        await _sut.UpsertFromSapAsync(new ItemsResponse { ItemCode = "RM2", ItemsGroupCode = 200 });

        var result = await _sut.ListFromDbAsync(
            new PaginationRequest { PageNumber = 1, PageSize = 10 },
            groupCodes: [100]);

        result.Data.Should().ContainSingle().Which.ItemCode.Should().Be("RM1");
    }

    [Test]
    public async Task SyncAllFromSapAsync_pages_by_item_code_and_reports_added_vs_updated()
    {
        await _sut.UpsertFromSapAsync(new ItemsResponse { ItemCode = "AAA", ItemName = "Existing" });

        _http
            .Setup(h => h.GetOrThrowAsync<SapItemsResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapItemsResponse
            {
                Value =
                [
                    new ItemsResponse { ItemCode = "AAA", ItemName = "Existing (updated)" },
                    new ItemsResponse { ItemCode = "BBB", ItemName = "New item" },
                ],
            });

        var result = await _sut.SyncAllFromSapAsync();

        result.AddedCount.Should().Be(1);
        result.UpdatedCount.Should().Be(1);
        result.LastItemCode.Should().Be("BBB");
        result.HasMore.Should().BeFalse();

        var rows = await _context.Items.Where(x => x.CompanyDb == CompanyDb).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Single(x => x.ItemCode == "AAA").ItemName.Should().Be("Existing (updated)");
    }

    [Test]
    public async Task SyncOneFromSapAsync_imports_a_new_item_then_refreshes_it()
    {
        _http
            .Setup(h => h.GetOrThrowAsync<SapItemsResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapItemsResponse { Value = [new ItemsResponse { ItemCode = "RM1", ItemName = "Raw Material 1" }] });

        var first = await _sut.SyncOneFromSapAsync("RM1");
        first.AddedCount.Should().Be(1);
        first.UpdatedCount.Should().Be(0);

        _http
            .Setup(h => h.GetOrThrowAsync<SapItemsResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapItemsResponse { Value = [new ItemsResponse { ItemCode = "RM1", ItemName = "Raw Material 1 (renamed)" }] });

        var second = await _sut.SyncOneFromSapAsync("RM1");
        second.AddedCount.Should().Be(0);
        second.UpdatedCount.Should().Be(1);

        var row = await _context.Items.SingleAsync(x => x.CompanyDb == CompanyDb && x.ItemCode == "RM1");
        row.ItemName.Should().Be("Raw Material 1 (renamed)");
    }

    [Test]
    public async Task SyncOneFromSapAsync_reports_when_sap_has_no_such_item()
    {
        _http
            .Setup(h => h.GetOrThrowAsync<SapItemsResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapItemsResponse { Value = [] });

        var result = await _sut.SyncOneFromSapAsync("MISSING");

        result.UpsertedCount.Should().Be(0);
        result.Message.Should().Contain("MISSING");
        (await _context.Items.AnyAsync(x => x.CompanyDb == CompanyDb)).Should().BeFalse();
    }

    [Test]
    public async Task GetSyncStateAsync_reflects_the_most_recent_sync()
    {
        _http
            .Setup(h => h.GetOrThrowAsync<SapItemsResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapItemsResponse { Value = [new ItemsResponse { ItemCode = "AAA" }] });

        await _sut.SyncAllFromSapAsync();
        var status = await _sut.GetSyncStateAsync();

        status.Should().NotBeNull();
        status!.LastItemCode.Should().Be("AAA");
        status.UpsertedCount.Should().Be(1);
    }
}
