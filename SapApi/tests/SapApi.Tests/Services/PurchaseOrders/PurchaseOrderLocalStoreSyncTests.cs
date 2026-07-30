using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Shared;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services.PurchaseOrders;

/// <summary>
/// The sync previously called IHttpRequestHandler.GetAsync, which swallows every failure and returns
/// default. A failed list page therefore ended the loop early and a failed detail fetch was logged and
/// skipped, so the user was told the sync succeeded while few or no purchase orders were imported.
/// This showed up on the live company DB (large dataset) but not on UAT. These tests pin the sync to
/// GetOrThrowAsync so SAP failures surface instead of being reported as success.
/// </summary>
[TestFixture]
public class PurchaseOrderLocalStoreSyncTests
{
    private const string CompanyDb = "PBBPL_LIVE";

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

        var companyDbAccessor = new Mock<ICurrentCompanyDbAccessor>();
        companyDbAccessor.Setup(x => x.GetCompanyDbName()).Returns(CompanyDb);

        _sut = new PurchaseOrderLocalStore(_context, _http.Object, companyDbAccessor.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private void SetupListPage(params int[] docEntries) =>
        _http.Setup(h => h.GetOrThrowAsync<GetAllSapPurchaseOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAllSapPurchaseOrdersResponse
            {
                Value = docEntries.Select(d => new SapPurchaseOrdersResponse { DocEntry = d }).ToList(),
            });

    private void SetupDetail(int docEntry, SapPurchaseOrdersResponse? detail) =>
        _http.Setup(h => h.GetOrThrowAsync<SapPurchaseOrdersResponse>(
                It.Is<string>(u => u.Contains($"({docEntry})")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

    [Test]
    public async Task SyncNew_imports_records_and_reports_success()
    {
        SetupListPage(101);
        SetupDetail(101, new SapPurchaseOrdersResponse { DocEntry = 101, DocNum = 5001, CardCode = "V001" });

        var result = await _sut.SyncNewFromSapAsync();

        result.AddedCount.Should().Be(1);
        result.UpsertedCount.Should().Be(1);
        _context.PurchaseOrders.Count().Should().Be(1);
    }

    [Test]
    public async Task SyncNew_throws_when_SAP_list_request_fails()
    {
        _http.Setup(h => h.GetOrThrowAsync<GetAllSapPurchaseOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiErrorException(BaseErrorCodes.ValidationFailed, "SAP session expired."));

        var act = async () => await _sut.SyncNewFromSapAsync();

        await act.Should().ThrowAsync<ApiErrorException>().WithMessage("*SAP session expired*");
    }

    [Test]
    public async Task SyncNew_throws_instead_of_reporting_success_when_list_payload_is_missing()
    {
        _http.Setup(h => h.GetOrThrowAsync<GetAllSapPurchaseOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAllSapPurchaseOrdersResponse { Value = null });

        var act = async () => await _sut.SyncNewFromSapAsync();

        await act.Should().ThrowAsync<ApiErrorException>();
        _context.PurchaseOrders.Count().Should().Be(0);
    }

    [Test]
    public async Task SyncNew_throws_instead_of_skipping_a_purchase_order_without_detail()
    {
        SetupListPage(101, 102);
        SetupDetail(101, new SapPurchaseOrdersResponse { DocEntry = 101, DocNum = 5001 });
        SetupDetail(102, null);

        var act = async () => await _sut.SyncNewFromSapAsync();

        await act.Should().ThrowAsync<ApiErrorException>().WithMessage("*102*");
    }

    [Test]
    public async Task SyncNew_records_the_failure_in_sync_state_so_status_cannot_claim_success()
    {
        SetupListPage(101, 102);
        SetupDetail(101, new SapPurchaseOrdersResponse { DocEntry = 101, DocNum = 5001 });
        SetupDetail(102, null);

        var act = async () => await _sut.SyncNewFromSapAsync();
        await act.Should().ThrowAsync<ApiErrorException>();

        var state = await _sut.GetSyncStateAsync();
        state.Should().NotBeNull();
        state!.Message.Should().Contain("Sync failed");
        // The purchase order fetched before the failure stays committed.
        _context.PurchaseOrders.Count().Should().Be(1);
    }

    [Test]
    public async Task SyncOne_surfaces_the_SAP_error_rather_than_a_generic_not_found()
    {
        _http.Setup(h => h.GetOrThrowAsync<SapPurchaseOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiErrorException(BaseErrorCodes.ValidationFailed, "SAP is unavailable."));

        var act = async () => await _sut.SyncOneFromSapAsync(101);

        await act.Should().ThrowAsync<ApiErrorException>().WithMessage("*SAP is unavailable*");
    }
}
