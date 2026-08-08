using System.Security.Claims;
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
using SapApi.Shared.Enums;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Tests.Services.PurchaseOrders;

namespace SapApi.Tests.Services;

/// <summary>
/// Verifies create flows only commit StageWisePayment (and approval drafts) when SAP succeeds
/// or approval queues PendingApproval — SAP failure rolls back the ambient transaction.
/// Uses SQLite so rollback is real (EF InMemory treats transactions as no-ops).
/// </summary>
[TestFixture]
public class StageWisePaymentCreateTransactionTests
{
    private const string CompanyDb = "PBBPL_UAT";
    private const int RequesterId = 1;
    private const int ApproverId = 10;

    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private UnitOfWork _unitOfWork = null!;
    private Mock<IHttpRequestHandler> _http = null!;
    private StageWisePaymentService _sut = null!;

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

        foreach (var id in new[] { RequesterId, ApproverId })
        {
            _context.Users.Add(new ApplicationUser
            {
                Id = id,
                UserName = $"user{id}@test.com",
                Email = $"user{id}@test.com",
                NormalizedUserName = $"USER{id}@TEST.COM",
                NormalizedEmail = $"USER{id}@TEST.COM",
            });
        }
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _unitOfWork = new UnitOfWork(_context);
        _http = new Mock<IHttpRequestHandler>();

        var companyDbAccessor = new Mock<ICurrentCompanyDbAccessor>();
        companyDbAccessor.Setup(x => x.GetCompanyDbName()).Returns(CompanyDb);

        var localStore = PurchaseOrderLocalStoreTestHelper.Create(_context, _http.Object, companyDbAccessor.Object);
        var purchaseOrderLinks = new PurchaseOrderLinkResolver(_context, companyDbAccessor.Object, localStore);

        var approvalService = new ApprovalService(
            _context,
            BuildHttpContextAccessor(RequesterId),
            companyDbAccessor.Object,
            purchaseOrderLinks);

        var seriesService = new SapDocumentSeriesService(_http.Object);
        var dpService = new SapPurchaseDownPaymentService(_http.Object, approvalService, seriesService);
        var vendorService = new SapVendorPaymentService(_http.Object, approvalService);

        _sut = new StageWisePaymentService(
            dpService,
            vendorService,
            _context,
            _unitOfWork,
            companyDbAccessor.Object,
            purchaseOrderLinks);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _unitOfWork.DisposeAsync();
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task CreateBatchDownPayment_WhenSapFails_RollsBackPaymentRow()
    {
        SetupSeriesLookup();
        _http.Setup(h => h.PostAsync<SapPurchaseDownPaymentRequest, SapPurchaseDownPaymentResponse>(
                It.Is<string>(u => u.Contains("PurchaseDownPayments") && !u.Contains("Series")),
                It.IsAny<SapPurchaseDownPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapPurchaseDownPaymentResponse
            {
                Error = new SapError
                {
                    Code = -2028,
                    Message = new SapMessage
                    {
                        Value = "To generate this document, first define the numbering series in the Administration module",
                    },
                },
            });

        var (success, message, payment) = await _sut.CreateBatchDownPaymentAsync(
            BuildPurchaseOrder(),
            [new StageWisePaymentBatchLineRequest { Amount = 1000, PaymentTermsTypes = [1], Bank = "HDFC" }],
            [new PaymentTermsUdf { Id = 1, Basic = 100, Gst = 0, Desc = "Advance" }],
            totalBasic: 10000,
            bank: "HDFC",
            wtCode: null,
            existingRecords: [],
            postingDate: DateTime.UtcNow.Date,
            paymentDate: DateTime.UtcNow.Date,
            persist: true);

        success.Should().BeFalse();
        payment.Should().BeNull();
        message.Should().Contain("numbering series");

        _context.ChangeTracker.Clear();
        (await _context.StageWisePayments.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await _context.ApprovalRequests.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task CreateBatchDownPayment_WhenSapSucceeds_PersistsPaymentRow()
    {
        SetupSeriesLookup();
        _http.Setup(h => h.PostAsync<SapPurchaseDownPaymentRequest, SapPurchaseDownPaymentResponse>(
                It.Is<string>(u => u.Contains("PurchaseDownPayments") && !u.Contains("Series")),
                It.IsAny<SapPurchaseDownPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapPurchaseDownPaymentResponse
            {
                DocEntry = 9001,
                DocNum = 501,
                WTAmount = 0,
            });

        var po = BuildPurchaseOrder();
        po.DocumentStatus = "bost_Close"; // skip outgoing payment follow-on

        var (success, message, payment) = await _sut.CreateBatchDownPaymentAsync(
            po,
            [new StageWisePaymentBatchLineRequest { Amount = 1000, PaymentTermsTypes = [1], Bank = "HDFC" }],
            [new PaymentTermsUdf { Id = 1, Basic = 100, Gst = 0, Desc = "Advance" }],
            totalBasic: 10000,
            bank: "HDFC",
            wtCode: null,
            existingRecords: [],
            postingDate: DateTime.UtcNow.Date,
            paymentDate: DateTime.UtcNow.Date,
            persist: true);

        success.Should().BeTrue(message);
        payment.Should().NotBeNull();
        payment!.Id.Should().BeGreaterThan(0);
        payment.Status.Should().Be(StageWisePaymentStatus.Added);
        payment.ApDownPaymentInvoiceEntryNumber.Should().Be("501");

        _context.ChangeTracker.Clear();
        var row = await _context.StageWisePayments.SingleAsync();
        row.Id.Should().Be(payment.Id);
        row.ApDownPaymentInvoiceEntryNumber.Should().Be("501");
        row.Status.Should().Be(StageWisePaymentStatus.Added);
    }

    [Test]
    public async Task CreateBatchDownPayment_WhenPendingApproval_PersistsPaymentAndApprovalRequest()
    {
        await SeedDownPaymentApprovalPolicyAsync();

        var (success, message, payment) = await _sut.CreateBatchDownPaymentAsync(
            BuildPurchaseOrder(),
            [new StageWisePaymentBatchLineRequest { Amount = 1000, PaymentTermsTypes = [1], Bank = "HDFC" }],
            [new PaymentTermsUdf { Id = 1, Basic = 100, Gst = 0, Desc = "Advance" }],
            totalBasic: 10000,
            bank: "HDFC",
            wtCode: null,
            existingRecords: [],
            postingDate: DateTime.UtcNow.Date,
            paymentDate: DateTime.UtcNow.Date,
            persist: true);

        success.Should().BeTrue(message);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(StageWisePaymentStatus.PendingApproval);
        payment.ApprovalRequestId.Should().NotBeNullOrWhiteSpace();

        _context.ChangeTracker.Clear();
        var row = await _context.StageWisePayments.SingleAsync();
        row.Status.Should().Be(StageWisePaymentStatus.PendingApproval);
        row.ApprovalRequestId.Should().Be(payment.ApprovalRequestId);

        var approval = await _context.ApprovalRequests.SingleAsync();
        approval.Id.ToString().Should().Be(payment.ApprovalRequestId);
        approval.DocumentType.Should().Be(ApprovalDocumentType.StagewisePayments_DP);
        approval.OverallStatus.Should().Be(ApprovalStatus.Pending);
    }

    private void SetupSeriesLookup()
    {
        var period = SapApi.Shared.Sap.SapDocumentSeriesResolver.GetIndiaFinancialYearPeriodIndicator(DateTime.UtcNow.Date);
        _http.Setup(h => h.PostAsync<object, SapDocumentSeriesListResponse>(
                It.Is<string>(u => u.Contains("SeriesService_GetDocumentSeries") || u.Contains("SeriesService")),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapDocumentSeriesListResponse
            {
                Value =
                [
                    new SapDocumentSeriesEntry
                    {
                        Series = 77,
                        BPLID = 1,
                        PeriodIndicator = period,
                        Locked = "tNO",
                        IsManual = "tNO",
                    },
                ],
            });
    }

    private async Task SeedDownPaymentApprovalPolicyAsync()
    {
        _context.ApprovalPolicies.Add(new ApprovalPolicy
        {
            CompanyDb = CompanyDb,
            DocumentType = ApprovalDocumentType.StagewisePayments_DP,
            RequesterUserId = RequesterId,
            RequesterType = ApprovalRequesterType.User,
            IsActive = true,
            Approvers = [new ApprovalPolicyApprover { ApproverUserId = ApproverId, Priority = 1 }],
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private static SapPurchaseOrdersResponse BuildPurchaseOrder() => new()
    {
        DocEntry = 42,
        DocNum = 1001,
        CardCode = "V001",
        DocTotal = 50000,
        VatSum = 0,
        BPLId = 1,
        DocumentStatus = "bost_Open",
        DocumentLines =
        [
            new SapInventoryTransferItemsRequests
            {
                LineNum = 0,
                ItemCode = "ITEM1",
                LineTotal = 10000,
                WarehouseCode = "WH01",
            },
        ],
    };

    private static IHttpContextAccessor BuildHttpContextAccessor(int userId)
    {
        var claims = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())]);
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(claims) };
        return new HttpContextAccessor { HttpContext = httpContext };
    }
}
