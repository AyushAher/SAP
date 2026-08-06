using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Shared;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Requests;
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

    /// <summary>Mirrors SAP paging: only DocEntries greater than the cursor in the request URL.</summary>
    private void SetupPagedList(params int[] docEntries) =>
        _http.Setup(h => h.GetOrThrowAsync<GetAllSapPurchaseOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) =>
            {
                var cursor = 0;
                var marker = "DocEntry gt ";
                var index = url.IndexOf(marker, StringComparison.Ordinal);
                if (index >= 0)
                    cursor = int.Parse(new string(url[(index + marker.Length)..].TakeWhile(char.IsDigit).ToArray()));

                return new GetAllSapPurchaseOrdersResponse
                {
                    Value = docEntries
                        .Where(d => d > cursor)
                        .OrderBy(d => d)
                        .Select(d => new SapPurchaseOrdersResponse { DocEntry = d })
                        .ToList(),
                };
            });

    private void SetupAnyDetail() =>
        _http.Setup(h => h.GetOrThrowAsync<SapPurchaseOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) =>
            {
                var docEntry = int.Parse(new string(url.Split('(').Last().TakeWhile(char.IsDigit).ToArray()));
                return new SapPurchaseOrdersResponse { DocEntry = docEntry, DocNum = 5000 + docEntry };
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
    public async Task SyncNew_reports_no_more_work_and_a_resume_cursor_for_a_small_dataset()
    {
        SetupPagedList(101, 102);
        SetupAnyDetail();

        var result = await _sut.SyncNewFromSapAsync();

        result.HasMore.Should().BeFalse();
        result.LastDocEntry.Should().Be(102);
        result.UpsertedCount.Should().Be(2);
    }

    [Test]
    public async Task SyncNew_resumes_from_afterDocEntry_without_reimporting_earlier_records()
    {
        SetupPagedList(101, 102, 103);
        SetupAnyDetail();

        var result = await _sut.SyncNewFromSapAsync(afterDocEntry: 102);

        result.UpsertedCount.Should().Be(1);
        result.LastDocEntry.Should().Be(103);
        _context.PurchaseOrders.Select(p => p.DocEntry).Should().BeEquivalentTo(new[] { 103 });
    }

    [Test]
    public async Task SyncNew_continues_across_batches_until_every_record_is_imported()
    {
        var docEntries = Enumerable.Range(1, 5).ToArray();
        SetupPagedList(docEntries);
        SetupAnyDetail();

        // Drive the same loop the UI performs: keep going while the server reports more work.
        int? cursor = null;
        var batches = 0;
        PurchaseOrderSyncResult result;
        do
        {
            result = await _sut.SyncNewFromSapAsync(cursor);
            cursor = result.LastDocEntry;
            batches++;
        }
        while (result.HasMore && batches < 10);

        result.HasMore.Should().BeFalse();
        _context.PurchaseOrders.Select(p => p.DocEntry).Should().BeEquivalentTo(docEntries);
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

    [Test]
    public async Task SyncOne_replaces_lines_when_po_already_exists_locally()
    {
        SeedLocalWithLines(101, ("ITEM-A", 0), ("ITEM-B", 1));
        SetupDetail(101, new SapPurchaseOrdersResponse
        {
            DocEntry = 101,
            DocNum = 5101,
            CardCode = "V001",
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests { LineNum = 0, ItemCode = "ITEM-A2" },
                new SapInventoryTransferItemsRequests { LineNum = 1, ItemCode = "ITEM-C" },
            ],
        });

        var result = await _sut.SyncOneFromSapAsync(101);

        result.UpdatedCount.Should().Be(1);
        var lines = await _context.PurchaseOrderLines
            .OrderBy(l => l.LineNum)
            .Select(l => new { l.LineNum, l.ItemCode, l.IsDeleted })
            .ToListAsync();
        lines.Should().HaveCount(2);
        lines[0].ItemCode.Should().Be("ITEM-A2");
        lines[1].ItemCode.Should().Be("ITEM-C");
        lines.Should().OnlyContain(l => !l.IsDeleted);
    }

    [Test]
    public void EnumerateIntegerGaps_yields_holes_after_cursor()
    {
        var gaps = PurchaseOrderLocalStore.EnumerateIntegerGaps([10, 12, 15], afterExclusive: 11).ToList();
        gaps.Should().Equal(13, 14);
    }

    [Test]
    public async Task SyncMissingGaps_restores_present_SAP_DocEntries_and_skips_absent()
    {
        SeedLocal(10, 13);
        SetupDetail(11, new SapPurchaseOrdersResponse { DocEntry = 11, DocNum = 5011, CardCode = "V011" });
        _http.Setup(h => h.GetOrThrowAsync<SapPurchaseOrdersResponse>(
                It.Is<string>(u => u.Contains("(12)")), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiErrorException(BaseErrorCodes.ValidationFailed, "Not found"));

        var result = await _sut.SyncMissingGapsFromSapAsync();

        result.Mode.Should().Be("gaps");
        result.HasMore.Should().BeFalse();
        result.AddedCount.Should().Be(1);
        _context.PurchaseOrders.Select(p => p.DocEntry).OrderBy(x => x).Should().Equal(10, 11, 13);
    }

    [Test]
    public async Task SyncMissingGaps_resumes_from_afterDocEntry()
    {
        SeedLocal(10, 15);
        SetupDetail(13, new SapPurchaseOrdersResponse { DocEntry = 13, DocNum = 5013, CardCode = "V013" });
        SetupDetail(14, new SapPurchaseOrdersResponse { DocEntry = 14, DocNum = 5014, CardCode = "V014" });
        _http.Setup(h => h.GetOrThrowAsync<SapPurchaseOrdersResponse>(
                It.Is<string>(u => u.Contains("(11)") || u.Contains("(12)")), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiErrorException(BaseErrorCodes.ValidationFailed, "Not found"));

        var result = await _sut.SyncMissingGapsFromSapAsync(afterDocEntry: 12);

        result.AddedCount.Should().Be(2);
        _context.PurchaseOrders.Select(p => p.DocEntry).OrderBy(x => x).Should().Equal(10, 13, 14, 15);
    }

    [Test]
    public async Task GetSyncState_clears_stale_Running_status_so_row_sync_ui_can_recover()
    {
        _context.PurchaseOrderSyncStates.Add(new SapApi.Domain.Entities.PurchaseOrderSyncState
        {
            CompanyDb = CompanyDb,
            Status = SapApi.Domain.Entities.PurchaseOrderSyncState.StatusRunning,
            StartedAtUtc = DateTime.UtcNow.AddHours(-3),
            LastSyncMessage = "Sync job queued…",
            HangfireJobId = "dead-job",
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var status = await _sut.GetSyncStateAsync();

        status.Should().NotBeNull();
        status!.Status.Should().Be(SapApi.Domain.Entities.PurchaseOrderSyncState.StatusFailed);
        status.Message.Should().Contain("still Running");
    }

    private void SeedLocal(params int[] docEntries)
    {
        var now = DateTime.UtcNow;
        foreach (var docEntry in docEntries)
        {
            _context.PurchaseOrders.Add(new SapApi.Domain.Entities.PurchaseOrder
            {
                CompanyDb = CompanyDb,
                DocEntry = docEntry,
                DocNum = 5000 + docEntry,
                SyncedAtUtc = now,
                CreatedOn = now,
                LastModifiedOn = now,
            });
        }

        _context.SaveChanges();
    }

    private void SeedLocalWithLines(int docEntry, params (string ItemCode, int LineNum)[] lines)
    {
        var now = DateTime.UtcNow;
        var po = new SapApi.Domain.Entities.PurchaseOrder
        {
            CompanyDb = CompanyDb,
            DocEntry = docEntry,
            DocNum = 5000 + docEntry,
            SyncedAtUtc = now,
            CreatedOn = now,
            LastModifiedOn = now,
            Lines = lines.Select(l => new SapApi.Domain.Entities.PurchaseOrderLine
            {
                LineNum = l.LineNum,
                ItemCode = l.ItemCode,
            }).ToList(),
        };
        _context.PurchaseOrders.Add(po);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }
}
