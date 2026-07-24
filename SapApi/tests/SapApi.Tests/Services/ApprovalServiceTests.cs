using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services;

/// <summary>
/// Production's AppDbContext pool is configured with QueryTrackingBehavior.NoTracking (see
/// DependencyInjection.cs) for scalability with large SAP datasets. That means every entity fetched
/// via a query is untracked by default, so any service that mutates a fetched entity's properties
/// MUST explicitly re-attach it (via DbSet.Update(...) or Entry(...).State = Modified) before
/// SaveChangesAsync, or the change is silently dropped even though SaveChangesAsync succeeds.
///
/// These tests configure the in-memory context the same way to guard against exactly that class of
/// bug: previously ApproveAsync/RejectAsync/EvaluateRequestStatus mutated UserApproval/ApprovalRequest
/// without re-attaching, so approvals appeared to succeed (audit log recorded, 200 OK returned) but the
/// request stayed stuck on ApprovalStatus.Pending forever and the SAP record was never created.
/// </summary>
[TestFixture]
public class ApprovalServiceTests
{
    private AppDbContext _context = null!;
    private ApprovalService _sut = null!;
    private const string CompanyDb = "PBBPL_UAT";
    private const int RequesterId = 1;
    private const int ApproverId = 10;
    private const int SecondApproverId = 20;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        foreach (var id in new[] { RequesterId, ApproverId, SecondApproverId })
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

        var companyDbAccessor = new Mock<ICurrentCompanyDbAccessor>();
        companyDbAccessor.Setup(x => x.GetCompanyDbName()).Returns(CompanyDb);

        _sut = new ApprovalService(_context, BuildHttpContextAccessor(RequesterId), companyDbAccessor.Object);
    }

    [TearDown]
    public async Task TearDown() => await _context.DisposeAsync();

    private static IHttpContextAccessor BuildHttpContextAccessor(int userId)
    {
        var claims = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())]);
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(claims) };
        return new HttpContextAccessor { HttpContext = httpContext };
    }

    private async Task<int> SeedRequestAsync(params int[] approverIdsInPriorityOrder)
    {
        var policy = new ApprovalPolicy
        {
            CompanyDb = CompanyDb,
            DocumentType = ApprovalDocumentType.Payments,
            RequesterUserId = RequesterId,
            IsActive = true,
        };
        _context.ApprovalPolicies.Add(policy);
        await _context.SaveChangesAsync();

        var request = new ApprovalRequest
        {
            CompanyDb = CompanyDb,
            DocumentType = ApprovalDocumentType.Payments,
            RequesterUserId = RequesterId,
            PolicyId = policy.Id,
            OverallStatus = ApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            RequestBody = "{}",
        };
        _context.ApprovalRequests.Add(request);
        await _context.SaveChangesAsync();

        var priority = 1;
        foreach (var approverId in approverIdsInPriorityOrder)
        {
            _context.UserApprovals.Add(new UserApproval
            {
                ApprovalRequestId = request.Id,
                UserId = approverId,
                Priority = priority++,
                ApprovalStatus = ApprovalStatus.Pending,
            });
        }
        await _context.SaveChangesAsync();

        // In production, ApproveAsync/RejectAsync always run against a freshly-pooled DbContext with
        // nothing tracked yet. Clear here so the shared test context matches that — otherwise the
        // entities we just Add()'d above (which stay tracked after SaveChangesAsync regardless of the
        // NoTracking query setting) would collide with the untracked instance ApproveAsync re-queries.
        _context.ChangeTracker.Clear();

        return request.Id;
    }

    [Test]
    public async Task ApproveAsync_SingleApproverLevel_PersistsApprovedStatus_ToDatabase()
    {
        var requestId = await SeedRequestAsync(ApproverId);

        var result = await _sut.ApproveAsync(requestId, ApproverId, "Approved");

        result!.OverallStatus.Should().Be(ApprovalStatus.Approved);
        result.IsApproved.Should().BeTrue();

        // Re-fetch from scratch (fresh query against the same store) to prove the write actually
        // landed in the database rather than only mutating the untracked in-memory instance.
        var persistedRequest = await _context.ApprovalRequests.AsNoTracking().FirstAsync(x => x.Id == requestId);
        persistedRequest.OverallStatus.Should().Be(ApprovalStatus.Approved);
        persistedRequest.IsApproved.Should().BeTrue();

        var persistedApproval = await _context.UserApprovals.AsNoTracking()
            .FirstAsync(x => x.ApprovalRequestId == requestId && x.UserId == ApproverId);
        persistedApproval.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        persistedApproval.Comment.Should().Be("Approved");
    }

    [Test]
    public async Task ApproveAsync_CalledTwiceByTheSameApprover_SecondCallThrowsConflict_InsteadOfSilentlyReapproving()
    {
        var requestId = await SeedRequestAsync(ApproverId);

        await _sut.ApproveAsync(requestId, ApproverId, "First approval");

        var act = async () => await _sut.ApproveAsync(requestId, ApproverId, "Second approval");

        await act.Should().ThrowAsync<ApiErrorException>().WithMessage("*already been processed*");
    }

    [Test]
    public async Task ApproveAsync_FirstOfTwoLevels_PersistsForwardedStatus_NotApproved()
    {
        var requestId = await SeedRequestAsync(ApproverId, SecondApproverId);

        var result = await _sut.ApproveAsync(requestId, ApproverId, "L1 approved");

        result!.OverallStatus.Should().Be(ApprovalStatus.Forwarded);

        var persistedRequest = await _context.ApprovalRequests.AsNoTracking().FirstAsync(x => x.Id == requestId);
        persistedRequest.OverallStatus.Should().Be(ApprovalStatus.Forwarded);

        var persistedApproval = await _context.UserApprovals.AsNoTracking()
            .FirstAsync(x => x.ApprovalRequestId == requestId && x.UserId == ApproverId);
        persistedApproval.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
    }

    [Test]
    public async Task ApproveAsync_SecondOfTwoLevels_PersistsApprovedStatus_AfterFirstLevelAlreadyApproved()
    {
        var requestId = await SeedRequestAsync(ApproverId, SecondApproverId);
        await _sut.ApproveAsync(requestId, ApproverId, "L1 approved");
        // Simulate the second approval arriving as its own HTTP request against a fresh pooled context.
        _context.ChangeTracker.Clear();

        var result = await _sut.ApproveAsync(requestId, SecondApproverId, "L2 approved");

        result!.OverallStatus.Should().Be(ApprovalStatus.Approved);
        result.IsApproved.Should().BeTrue();

        var persistedRequest = await _context.ApprovalRequests.AsNoTracking().FirstAsync(x => x.Id == requestId);
        persistedRequest.OverallStatus.Should().Be(ApprovalStatus.Approved);
        persistedRequest.IsApproved.Should().BeTrue();
    }

    [Test]
    public async Task RejectAsync_PersistsRejectedStatus_ToDatabase()
    {
        var requestId = await SeedRequestAsync(ApproverId);

        await _sut.RejectAsync(requestId, ApproverId, "Not valid");

        var persistedRequest = await _context.ApprovalRequests.AsNoTracking().FirstAsync(x => x.Id == requestId);
        persistedRequest.OverallStatus.Should().Be(ApprovalStatus.Rejected);
        persistedRequest.IsApproved.Should().BeFalse();

        var persistedApproval = await _context.UserApprovals.AsNoTracking()
            .FirstAsync(x => x.ApprovalRequestId == requestId && x.UserId == ApproverId);
        persistedApproval.ApprovalStatus.Should().Be(ApprovalStatus.Rejected);
        persistedApproval.Comment.Should().Be("Not valid");
    }

    [Test]
    public async Task ApproveAsync_UnknownApprover_ThrowsForbidden()
    {
        var requestId = await SeedRequestAsync(ApproverId);

        var act = async () => await _sut.ApproveAsync(requestId, SecondApproverId, "Approved");

        await act.Should().ThrowAsync<ApiErrorException>().WithMessage("*not an approver*");
    }

    /// <summary>
    /// SapVendorPaymentRequests (the object actually evaluated for Payments) has no DocTotal property —
    /// only TransferSum (a string, the batch's own outgoing payment total). A "DocTotal" rule on a
    /// Payments policy must resolve against TransferSum instead of silently no-op'ing via a missing
    /// PropertyInfo (which would make the threshold never actually gate anything).
    /// </summary>
    private async Task SeedPaymentsPolicyWithDocTotalRuleAsync(string @operator, string thresholdValue)
    {
        var policy = new ApprovalPolicy
        {
            CompanyDb = CompanyDb,
            DocumentType = ApprovalDocumentType.Payments,
            RequesterUserId = RequesterId,
            IsActive = true,
            Approvers = [new ApprovalPolicyApprover { ApproverUserId = ApproverId, Priority = 1 }],
            Rules = [new ApprovalPolicyRule { FieldName = "DocTotal", Operator = @operator, Value = thresholdValue }],
        };
        _context.ApprovalPolicies.Add(policy);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    [Test]
    public async Task CheckApprovalPolicy_PaymentsDocTotalRule_TransferSumAboveThreshold_RequiresApproval()
    {
        await SeedPaymentsPolicyWithDocTotalRuleAsync("GreaterThan", "100000");
        var payment = new SapVendorPaymentRequests { CardCode = "V001", TransferSum = "150000.00" };

        var result = await _sut.CheckApprovalPolicy<SapVendorPaymentRequests>(
            null, payment, ApprovalDocumentType.Payments, ApprovalAction.Create);

        result.PendingApproval.Should().BeTrue();
    }

    [Test]
    public async Task CheckApprovalPolicy_PaymentsDocTotalRule_TransferSumAtOrBelowThreshold_SkipsApproval()
    {
        await SeedPaymentsPolicyWithDocTotalRuleAsync("GreaterThan", "100000");
        var payment = new SapVendorPaymentRequests { CardCode = "V001", TransferSum = "50000.00" };

        var result = await _sut.CheckApprovalPolicy<SapVendorPaymentRequests>(
            null, payment, ApprovalDocumentType.Payments, ApprovalAction.Create);

        result.PendingApproval.Should().BeFalse();
    }

    [Test]
    public async Task CheckApprovalPolicy_GroupPolicy_MatchesMemberAndRequiresApproval()
    {
        _context.UserGroups.Add(new UserGroup
        {
            CompanyDb = CompanyDb,
            Name = "Buyers",
            IsActive = true,
            Members = [new UserGroupMember { UserId = RequesterId }],
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var group = await _context.UserGroups.FirstAsync();
        _context.ApprovalPolicies.Add(new ApprovalPolicy
        {
            CompanyDb = CompanyDb,
            DocumentType = ApprovalDocumentType.Payments,
            RequesterType = ApprovalRequesterType.Group,
            RequesterGroupId = group.Id,
            IsActive = true,
            Approvers = [new ApprovalPolicyApprover { ApproverUserId = ApproverId, Priority = 1 }],
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _sut.CheckApprovalPolicy(
            null,
            new SapVendorPaymentRequests { CardCode = "V001", TransferSum = "1" },
            ApprovalDocumentType.Payments,
            ApprovalAction.Create);

        result.PendingApproval.Should().BeTrue();
    }

    [Test]
    public async Task CheckApprovalPolicy_UserPolicyTakesPriorityOverGroupPolicy()
    {
        _context.UserGroups.Add(new UserGroup
        {
            CompanyDb = CompanyDb,
            Name = "Buyers",
            IsActive = true,
            Members = [new UserGroupMember { UserId = RequesterId }],
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var group = await _context.UserGroups.FirstAsync();
        _context.ApprovalPolicies.Add(new ApprovalPolicy
        {
            CompanyDb = CompanyDb,
            DocumentType = ApprovalDocumentType.Payments,
            RequesterType = ApprovalRequesterType.Group,
            RequesterGroupId = group.Id,
            IsActive = true,
            Approvers = [new ApprovalPolicyApprover { ApproverUserId = ApproverId, Priority = 1 }],
            Rules = [new ApprovalPolicyRule { FieldName = "DocTotal", Operator = "GreaterThan", Value = "1" }],
        });
        // User-specific policy with a rule that will NOT match — proves user policy wins over group.
        _context.ApprovalPolicies.Add(new ApprovalPolicy
        {
            CompanyDb = CompanyDb,
            DocumentType = ApprovalDocumentType.Payments,
            RequesterType = ApprovalRequesterType.User,
            RequesterUserId = RequesterId,
            IsActive = true,
            Approvers = [new ApprovalPolicyApprover { ApproverUserId = ApproverId, Priority = 1 }],
            Rules = [new ApprovalPolicyRule { FieldName = "DocTotal", Operator = "GreaterThan", Value = "999999" }],
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _sut.CheckApprovalPolicy(
            null,
            new SapVendorPaymentRequests { CardCode = "V001", TransferSum = "100" },
            ApprovalDocumentType.Payments,
            ApprovalAction.Create);

        // Group would have matched (100 > 1), but user policy is preferred and its rule fails (100 !> 999999).
        result.PendingApproval.Should().BeFalse();
    }

    [Test]
    public async Task CheckApprovalPolicy_PurchaseOrderDocTotalAboveThreshold_RequiresApproval()
    {
        _context.ApprovalPolicies.Add(new ApprovalPolicy
        {
            CompanyDb = CompanyDb,
            DocumentType = ApprovalDocumentType.PurchaseOrder,
            RequesterUserId = RequesterId,
            IsActive = true,
            Approvers = [new ApprovalPolicyApprover { ApproverUserId = ApproverId, Priority = 1 }],
            Rules = [new ApprovalPolicyRule { FieldName = "DocTotal", Operator = "GreaterThan", Value = "1000000" }],
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var po = new SapPurchaseOrdersResponse
        {
            CardCode = "V001",
            DocTotal = 1_500_000,
        };

        var result = await _sut.CheckApprovalPolicy(
            null, po, ApprovalDocumentType.PurchaseOrder, ApprovalAction.Create);

        result.PendingApproval.Should().BeTrue();
        result.PendingApprovalRequestId.Should().NotBeNull();
    }
}
