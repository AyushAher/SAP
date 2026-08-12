using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services.ProductionOrders;
using SapApi.Shared;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services.ProductionOrders;

/// <summary>
/// Covers the production order mirror: incremental and resumable bulk sync, row-level sync of one
/// order, gap fill, and the open-order refresh that keeps the Issue / Receipt from Production
/// pickers accurate (SAP exposes no last-changed field on ProductionOrders).
/// </summary>
[TestFixture]
public class ProductionOrderLocalStoreSyncTests
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

        var companyDbAccessor = new Mock<ICurrentCompanyDbAccessor>();
        companyDbAccessor.Setup(x => x.GetCompanyDbName()).Returns(CompanyDb);

        _sut = ProductionOrderLocalStoreTestHelper.Create(_context, _http.Object, companyDbAccessor.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    /// <summary>Mirrors SAP paging: only AbsoluteEntries greater than the cursor in the request URL.</summary>
    private void SetupPagedList(params int[] absoluteEntries) =>
        _http.Setup(h => h.GetOrThrowAsync<GetAllSapProductionOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) =>
            {
                var cursor = 0;
                const string marker = "AbsoluteEntry gt ";
                var index = url.IndexOf(marker, StringComparison.Ordinal);
                if (index >= 0)
                    cursor = int.Parse(new string(url[(index + marker.Length)..].TakeWhile(char.IsDigit).ToArray()));

                return new GetAllSapProductionOrdersResponse
                {
                    Value = absoluteEntries
                        .Where(e => e > cursor)
                        .OrderBy(e => e)
                        .Select(e => new SapProductionOrdersResponse { AbsoluteEntry = e })
                        .ToList(),
                };
            });

    private void SetupAnyDetail(string status = Constants.SapProductionOrderStatus.Released) =>
        _http.Setup(h => h.GetOrThrowAsync<SapProductionOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) =>
            {
                var entry = int.Parse(new string(url.Split('(').Last().TakeWhile(char.IsDigit).ToArray()));
                return new SapProductionOrdersResponse
                {
                    AbsoluteEntry = entry,
                    DocumentNumber = 900000 + entry,
                    Status = status,
                };
            });

    private void SetupDetail(int absoluteEntry, SapProductionOrdersResponse? detail) =>
        _http.Setup(h => h.GetOrThrowAsync<SapProductionOrdersResponse>(
                It.Is<string>(u => u.Contains($"({absoluteEntry})")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

    [Test]
    public async Task SyncNew_imports_records_with_their_lines()
    {
        SetupPagedList(101);
        SetupDetail(101, new SapProductionOrdersResponse
        {
            AbsoluteEntry = 101,
            DocumentNumber = 900101,
            Status = Constants.SapProductionOrderStatus.Released,
            CustomerCode = "C001",
            Project = "P001",
            ProjectName = "Bridge upgrade",
            SalesOrderDocNum = 252610001,
            Warehouse = "WIP",
            ProductionOrderLines =
            [
                new SapProductionOrderLines { LineNumber = 1, ItemNo = "RM-1", PlannedQuantity = 5 },
                new SapProductionOrderLines { LineNumber = 2, ItemNo = "RM-2", PlannedQuantity = 7 },
            ],
        });

        var result = await _sut.SyncNewFromSapAsync();

        result.AddedCount.Should().Be(1);
        result.UpsertedCount.Should().Be(1);

        var order = await _context.ProductionOrders.SingleAsync();
        order.DocumentNumber.Should().Be(900101);
        order.SalesOrderDocNum.Should().Be(252610001);
        order.ProjectName.Should().Be("Bridge upgrade");
        _context.ProductionOrderLines.Count().Should().Be(2);
    }

    [Test]
    public async Task SyncNew_throws_when_SAP_list_request_fails()
    {
        _http.Setup(h => h.GetOrThrowAsync<GetAllSapProductionOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiErrorException(BaseErrorCodes.ValidationFailed, "SAP session expired."));

        var act = async () => await _sut.SyncNewFromSapAsync();

        await act.Should().ThrowAsync<ApiErrorException>().WithMessage("*SAP session expired*");
    }

    [Test]
    public async Task SyncNew_throws_instead_of_reporting_success_when_list_payload_is_missing()
    {
        _http.Setup(h => h.GetOrThrowAsync<GetAllSapProductionOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAllSapProductionOrdersResponse { Value = null });

        var act = async () => await _sut.SyncNewFromSapAsync();

        await act.Should().ThrowAsync<ApiErrorException>();
        _context.ProductionOrders.Count().Should().Be(0);
    }

    [Test]
    public async Task SyncNew_throws_instead_of_skipping_an_order_without_detail()
    {
        SetupPagedList(101, 102);
        SetupDetail(101, new SapProductionOrdersResponse { AbsoluteEntry = 101 });
        SetupDetail(102, null);

        var act = async () => await _sut.SyncNewFromSapAsync();

        await act.Should().ThrowAsync<ApiErrorException>().WithMessage("*102*");
    }

    [Test]
    public async Task SyncNew_records_the_failure_in_sync_state_so_status_cannot_claim_success()
    {
        SetupPagedList(101, 102);
        SetupDetail(101, new SapProductionOrdersResponse { AbsoluteEntry = 101 });
        SetupDetail(102, null);

        var act = async () => await _sut.SyncNewFromSapAsync();
        await act.Should().ThrowAsync<ApiErrorException>();

        var state = await _sut.GetSyncStateAsync();
        state.Should().NotBeNull();
        state!.Message.Should().Contain("Sync failed");
        _context.ProductionOrders.Count().Should().Be(1);
    }

    [Test]
    public async Task SyncNew_resumes_from_the_cursor_without_reimporting_earlier_records()
    {
        SetupPagedList(101, 102, 103);
        SetupAnyDetail();

        var result = await _sut.SyncNewFromSapAsync(afterAbsoluteEntry: 102);

        result.UpsertedCount.Should().Be(1);
        result.LastAbsoluteEntry.Should().Be(103);
        _context.ProductionOrders.Select(p => p.AbsoluteEntry).Should().BeEquivalentTo(new[] { 103 });
    }

    [Test]
    public async Task SyncNew_starts_after_the_local_max_entry()
    {
        SeedLocal((101, Constants.SapProductionOrderStatus.Closed));
        SetupPagedList(101, 102);
        SetupAnyDetail();

        var result = await _sut.SyncNewFromSapAsync();

        result.AddedCount.Should().Be(1);
        _context.ProductionOrders.Select(p => p.AbsoluteEntry).OrderBy(x => x).Should().Equal(101, 102);
    }

    [Test]
    public async Task SyncNew_continues_across_batches_until_every_record_is_imported()
    {
        var entries = Enumerable.Range(1, 5).ToArray();
        SetupPagedList(entries);
        SetupAnyDetail();

        int? cursor = null;
        var batches = 0;
        ProductionOrderSyncResult result;
        do
        {
            result = await _sut.SyncNewFromSapAsync(cursor);
            cursor = result.LastAbsoluteEntry;
            batches++;
        }
        while (result.HasMore && batches < 10);

        result.HasMore.Should().BeFalse();
        _context.ProductionOrders.Select(p => p.AbsoluteEntry).Should().BeEquivalentTo(entries);
    }

    [Test]
    public async Task SyncOne_adds_a_single_order_and_writes_an_audit_row()
    {
        SetupDetail(101, new SapProductionOrdersResponse
        {
            AbsoluteEntry = 101,
            DocumentNumber = 900101,
            Status = Constants.SapProductionOrderStatus.Released,
        });

        var result = await _sut.SyncOneFromSapAsync(101);

        result.Mode.Should().Be("one");
        result.AddedCount.Should().Be(1);
        result.AbsoluteEntry.Should().Be(101);
        result.Message.Should().Contain("900101");

        var audit = await _context.ProductionOrderSyncLogs.SingleAsync();
        audit.Mode.Should().Be("one");
        audit.AbsoluteEntry.Should().Be(101);
        audit.Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task SyncOne_updates_status_and_quantities_on_an_existing_row()
    {
        SeedLocal((101, Constants.SapProductionOrderStatus.Planned));
        SetupDetail(101, new SapProductionOrdersResponse
        {
            AbsoluteEntry = 101,
            DocumentNumber = 900101,
            Status = Constants.SapProductionOrderStatus.Released,
            PlannedQuantity = 12,
            CompletedQuantity = 4,
        });

        var result = await _sut.SyncOneFromSapAsync(101);

        result.UpdatedCount.Should().Be(1);
        var order = await _context.ProductionOrders.SingleAsync(p => p.AbsoluteEntry == 101);
        order.Status.Should().Be(Constants.SapProductionOrderStatus.Released);
        order.PlannedQuantity.Should().Be(12);
        order.CompletedQuantity.Should().Be(4);
    }

    [Test]
    public async Task SyncOne_replaces_lines_and_keeps_the_numeric_UoM_code()
    {
        SeedLocalWithLines(101, ("RM-A", 1), ("RM-B", 2));
        SetupDetail(101, new SapProductionOrdersResponse
        {
            AbsoluteEntry = 101,
            DocumentNumber = 900101,
            ProductionOrderLines =
            [
                new SapProductionOrderLines { LineNumber = 1, ItemNo = "RM-A2", UoMCode = -1 },
                new SapProductionOrderLines { LineNumber = 2, ItemNo = "RM-C", UoMCode = 7 },
            ],
        });

        await _sut.SyncOneFromSapAsync(101);

        var lines = await _context.ProductionOrderLines
            .OrderBy(l => l.LineNumber)
            .Select(l => new { l.ItemNo, l.UoMCode, l.IsDeleted })
            .ToListAsync();
        lines.Should().HaveCount(2);
        lines[0].ItemNo.Should().Be("RM-A2");
        lines[0].UoMCode.Should().Be(-1);
        lines[1].UoMCode.Should().Be(7);
        lines.Should().OnlyContain(l => !l.IsDeleted);
    }

    [Test]
    public async Task SyncOne_surfaces_the_SAP_error_rather_than_a_generic_not_found()
    {
        _http.Setup(h => h.GetOrThrowAsync<SapProductionOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiErrorException(BaseErrorCodes.ValidationFailed, "SAP is unavailable."));

        var act = async () => await _sut.SyncOneFromSapAsync(101);

        await act.Should().ThrowAsync<ApiErrorException>().WithMessage("*SAP is unavailable*");
        var audit = await _context.ProductionOrderSyncLogs.SingleAsync();
        audit.Succeeded.Should().BeFalse();
    }

    [Test]
    public async Task SyncOne_throws_when_SAP_has_no_such_production_order()
    {
        SetupDetail(101, null);

        var act = async () => await _sut.SyncOneFromSapAsync(101);

        await act.Should().ThrowAsync<ApiErrorException>().WithMessage("*was not found in SAP*");
    }

    [Test]
    public void EnumerateIntegerGaps_yields_holes_after_cursor()
    {
        var gaps = ProductionOrderLocalStore.EnumerateIntegerGaps([10, 12, 15], afterExclusive: 11).ToList();
        gaps.Should().Equal(13, 14);
    }

    [Test]
    public async Task SyncMissingGaps_restores_present_entries_and_skips_absent()
    {
        SeedLocal((10, null), (13, null));
        SetupDetail(11, new SapProductionOrdersResponse { AbsoluteEntry = 11, DocumentNumber = 900011 });
        _http.Setup(h => h.GetOrThrowAsync<SapProductionOrdersResponse>(
                It.Is<string>(u => u.Contains("(12)")), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiErrorException(BaseErrorCodes.ValidationFailed, "Not found"));

        var result = await _sut.SyncMissingGapsFromSapAsync();

        result.Mode.Should().Be("gaps");
        result.HasMore.Should().BeFalse();
        result.AddedCount.Should().Be(1);
        _context.ProductionOrders.Select(p => p.AbsoluteEntry).OrderBy(x => x).Should().Equal(10, 11, 13);
    }

    [Test]
    public async Task SyncOpenOrders_refreshes_only_planned_and_released_rows()
    {
        SeedLocal(
            (10, Constants.SapProductionOrderStatus.Planned),
            (11, Constants.SapProductionOrderStatus.Released),
            (12, Constants.SapProductionOrderStatus.Closed),
            (13, Constants.SapProductionOrderStatus.Cancelled));
        SetupAnyDetail(Constants.SapProductionOrderStatus.Closed);

        var result = await _sut.SyncOpenOrdersFromSapAsync();

        result.Mode.Should().Be("open");
        result.UpdatedCount.Should().Be(2);
        _http.Verify(
            h => h.GetOrThrowAsync<SapProductionOrdersResponse>(
                It.Is<string>(u => u.Contains("(12)") || u.Contains("(13)")),
                It.IsAny<CancellationToken>()),
            Times.Never);

        var statuses = await _context.ProductionOrders
            .OrderBy(p => p.AbsoluteEntry)
            .Select(p => p.Status)
            .ToListAsync();
        statuses.Should().Equal(
            Constants.SapProductionOrderStatus.Closed,
            Constants.SapProductionOrderStatus.Closed,
            Constants.SapProductionOrderStatus.Closed,
            Constants.SapProductionOrderStatus.Cancelled);
    }

    [Test]
    public async Task SyncOpenOrders_resumes_from_the_cursor()
    {
        SeedLocal(
            (10, Constants.SapProductionOrderStatus.Released),
            (11, Constants.SapProductionOrderStatus.Released));
        SetupAnyDetail();

        var result = await _sut.SyncOpenOrdersFromSapAsync(afterAbsoluteEntry: 10);

        result.UpdatedCount.Should().Be(1);
        _http.Verify(
            h => h.GetOrThrowAsync<SapProductionOrdersResponse>(
                It.Is<string>(u => u.Contains("(10)")), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task GetSyncState_clears_stale_Running_status_so_row_sync_ui_can_recover()
    {
        _context.ProductionOrderSyncStates.Add(new ProductionOrderSyncState
        {
            CompanyDb = CompanyDb,
            Status = ProductionOrderSyncState.StatusRunning,
            StartedAtUtc = DateTime.UtcNow.AddHours(-3),
            LastSyncMessage = "Sync job queued…",
            HangfireJobId = "dead-job",
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var status = await _sut.GetSyncStateAsync();

        status.Should().NotBeNull();
        status!.Status.Should().Be(ProductionOrderSyncState.StatusFailed);
        status.Message.Should().Contain("still Running");
    }

    private void SeedLocal(params (int AbsoluteEntry, string? Status)[] orders)
    {
        var now = DateTime.UtcNow;
        foreach (var (absoluteEntry, status) in orders)
        {
            _context.ProductionOrders.Add(new ProductionOrder
            {
                CompanyDb = CompanyDb,
                AbsoluteEntry = absoluteEntry,
                DocumentNumber = 900000 + absoluteEntry,
                Status = status,
                SyncedAtUtc = now,
                CreatedOn = now,
                LastModifiedOn = now,
            });
        }

        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    private void SeedLocalWithLines(int absoluteEntry, params (string ItemNo, int LineNumber)[] lines)
    {
        var now = DateTime.UtcNow;
        _context.ProductionOrders.Add(new ProductionOrder
        {
            CompanyDb = CompanyDb,
            AbsoluteEntry = absoluteEntry,
            DocumentNumber = 900000 + absoluteEntry,
            SyncedAtUtc = now,
            CreatedOn = now,
            LastModifiedOn = now,
            Lines = lines.Select(l => new ProductionOrderLine
            {
                LineNumber = l.LineNumber,
                ItemNo = l.ItemNo,
            }).ToList(),
        });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }
}
