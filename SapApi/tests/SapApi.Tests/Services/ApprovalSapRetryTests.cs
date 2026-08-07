using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Tests.Services.PurchaseOrders;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;

namespace SapApi.Tests.Services;

[TestFixture]
public class ApprovalSapRetryTests
{
    private AppDbContext _context = null!;
    private ApprovalService _sut = null!;
    private const string CompanyDb = "PBBPL_UAT";
    private const int RequesterId = 1;
    private const int ApproverId = 10;
    private const int OutsiderId = 99;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        foreach (var id in new[] { RequesterId, ApproverId, OutsiderId })
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

        _sut = CreateService(RequesterId);
    }

    [TearDown]
    public async Task TearDown() => await _context.DisposeAsync();

    private ApprovalService CreateService(int userId)
    {
        var companyDbAccessor = new Mock<ICurrentCompanyDbAccessor>();
        companyDbAccessor.Setup(x => x.GetCompanyDbName()).Returns(CompanyDb);

        var requestHandler = new Mock<IHttpRequestHandler>();
        var localStore = PurchaseOrderLocalStoreTestHelper.Create(_context, requestHandler.Object, companyDbAccessor.Object);
        var purchaseOrderLinks = new PurchaseOrderLinkResolver(_context, companyDbAccessor.Object, localStore);

        return new ApprovalService(
            _context,
            BuildHttpContextAccessor(userId),
            companyDbAccessor.Object,
            purchaseOrderLinks);
    }

    private static IHttpContextAccessor BuildHttpContextAccessor(int userId)
    {
        var claims = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())]);
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(claims) };
        return new HttpContextAccessor { HttpContext = httpContext };
    }

    private async Task<int> SeedFailedRequestAsync(
        int requesterId = RequesterId,
        string? sapDocEntry = null,
        string? requestBody = "{\"CardCode\":\"V001\"}",
        ApprovalStatus overallStatus = ApprovalStatus.Failed)
    {
        var policy = new ApprovalPolicy
        {
            CompanyDb = CompanyDb,
            DocumentType = ApprovalDocumentType.PurchaseOrder,
            RequesterUserId = requesterId,
            IsActive = true,
        };
        _context.ApprovalPolicies.Add(policy);
        await _context.SaveChangesAsync();

        var request = new ApprovalRequest
        {
            CompanyDb = CompanyDb,
            PolicyId = policy.Id,
            DocumentType = ApprovalDocumentType.PurchaseOrder,
            RequesterUserId = requesterId,
            Action = ApprovalAction.Create,
            IsApproved = true,
            OverallStatus = overallStatus,
            RequestBody = requestBody,
            FailureReason = overallStatus == ApprovalStatus.Failed ? "SAP timeout" : null,
            SapResponseDocEntry = sapDocEntry,
        };
        _context.ApprovalRequests.Add(request);
        await _context.SaveChangesAsync();

        _context.UserApprovals.Add(new UserApproval
        {
            ApprovalRequestId = request.Id,
            UserId = ApproverId,
            Priority = 1,
            ApprovalStatus = ApprovalStatus.Approved,
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        return request.Id;
    }

    [TestCase(ApprovalStatus.Failed, null, "{\"CardCode\":\"V001\"}", true)]
    [TestCase(ApprovalStatus.Approved, null, "{\"CardCode\":\"V001\"}", true)]
    [TestCase(ApprovalStatus.Failed, "123", "{\"CardCode\":\"V001\"}", false)]
    [TestCase(ApprovalStatus.Pending, null, "{\"CardCode\":\"V001\"}", false)]
    [TestCase(ApprovalStatus.Rejected, null, "{\"CardCode\":\"V001\"}", false)]
    [TestCase(ApprovalStatus.Failed, null, null, false)]
    public void IsEligibleForSapRetry_MatchesExpectedStates(
        ApprovalStatus status,
        string? sapDocEntry,
        string? requestBody,
        bool expected)
    {
        var request = new ApprovalRequest
        {
            IsApproved = status is ApprovalStatus.Failed or ApprovalStatus.Approved,
            OverallStatus = status,
            SapResponseDocEntry = sapDocEntry,
            RequestBody = requestBody,
        };

        ApprovalService.IsEligibleForSapRetry(request).Should().Be(expected);
    }

    [Test]
    public async Task GetRequestForSapRetryAsync_Requester_AllowsEligibleFailedRequest()
    {
        var requestId = await SeedFailedRequestAsync();

        var result = await _sut.GetRequestForSapRetryAsync(requestId, RequesterId, isAdmin: false);

        result.Id.Should().Be(requestId);
    }

    [Test]
    public async Task GetRequestForSapRetryAsync_Approver_AllowsEligibleFailedRequest()
    {
        var requestId = await SeedFailedRequestAsync();
        var approverService = CreateService(ApproverId);

        var result = await approverService.GetRequestForSapRetryAsync(requestId, ApproverId, isAdmin: false);

        result.Id.Should().Be(requestId);
    }

    [Test]
    public async Task GetRequestForSapRetryAsync_Outsider_ThrowsForbidden()
    {
        var requestId = await SeedFailedRequestAsync();
        var outsiderService = CreateService(OutsiderId);

        var act = async () => await outsiderService.GetRequestForSapRetryAsync(requestId, OutsiderId, isAdmin: false);

        await act.Should().ThrowAsync<ApiErrorException>().WithMessage("*not authorized*");
    }

    [Test]
    public async Task GetRequestForSapRetryAsync_WhenSapDocEntryExists_ThrowsValidationFailed()
    {
        var requestId = await SeedFailedRequestAsync(sapDocEntry: "456");

        var act = async () => await _sut.GetRequestForSapRetryAsync(requestId, RequesterId, isAdmin: false);

        await act.Should().ThrowAsync<ApiErrorException>().WithMessage("*already has a SAP document*");
    }

    [Test]
    public async Task MarkSapRetrySucceededAsync_ClearsFailureAndSetsApproved()
    {
        var requestId = await SeedFailedRequestAsync();

        await _sut.MarkSapRetrySucceededAsync(requestId);

        var persisted = await _context.ApprovalRequests.AsNoTracking().FirstAsync(x => x.Id == requestId);
        persisted.OverallStatus.Should().Be(ApprovalStatus.Approved);
        persisted.FailureReason.Should().BeNull();

        var log = await _context.ApprovalLogs.AsNoTracking()
            .FirstAsync(x => x.ApprovalRequestId == requestId && x.Action == "SapRetrySucceeded");
        log.Comment.Should().Be("SAP posting succeeded on retry.");
    }
}
