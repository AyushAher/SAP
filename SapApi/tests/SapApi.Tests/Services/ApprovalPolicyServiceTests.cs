using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Enums;

namespace SapApi.Tests.Services;

[TestFixture]
public class ApprovalPolicyServiceTests
{
    private AppDbContext _context = null!;
    private ApprovalPolicyService _sut = null!;

    [SetUp]
    public async Task SetUp()
    {
        // Mirrors production's DbContextPool configuration (QueryTrackingBehavior.NoTracking, see
        // DependencyInjection.cs) so tests catch bugs where a fetched entity is mutated without being
        // explicitly re-attached (DbSet.Update/Entry.State=Modified) before SaveChangesAsync.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
        await SeedUsersAsync(1, 2, 5, 10, 20, 30, 40);

        var companyDbAccessor = new Mock<ICurrentCompanyDbAccessor>();
        companyDbAccessor.Setup(x => x.GetCompanyDb()).Returns(SapCompanyDatabase.PBBPL_UAT);
        companyDbAccessor.Setup(x => x.GetCompanyDbName()).Returns(SapCompanyDatabase.PBBPL_UAT.ToString());

        _sut = new ApprovalPolicyService(_context, companyDbAccessor.Object);
    }

    private async Task SeedUsersAsync(params int[] userIds)
    {
        foreach (var id in userIds)
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
    }

    [TearDown]
    public async Task TearDown() => await _context.DisposeAsync();

    [Test]
    public async Task CreatePolicyAsync_ValidApprovers_PersistsPolicy()
    {
        var approvers = new List<ApprovalPolicyApprover>
        {
            new() { ApproverUserId = 10, Priority = 1 },
            new() { ApproverUserId = 20, Priority = 2 },
        };

        var policyId = await _sut.CreatePolicyAsync(
            ApprovalDocumentType.PurchaseOrder,
            ApprovalRequesterType.User,
            5,
            null,
            approvers);

        policyId.Should().BeGreaterThan(0);
        var saved = await _sut.GetByIdAsync(policyId);
        saved.Should().NotBeNull();
        saved!.DocumentType.Should().Be(ApprovalDocumentType.PurchaseOrder);
        saved.RequesterUserId.Should().Be(5);
        saved.Approvers.Should().HaveCount(2);
        saved.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task CreatePolicyAsync_EmptyApprovers_Throws()
    {
        var act = async () => await _sut.CreatePolicyAsync(
            ApprovalDocumentType.PurchaseOrder,
            ApprovalRequesterType.User,
            1,
            null,
            []);

        await act.Should().ThrowAsync<Exception>().WithMessage("*At least one approver required*");
    }

    [Test]
    public async Task CreatePolicyAsync_DuplicateApprovers_Throws()
    {
        var approvers = new List<ApprovalPolicyApprover>
        {
            new() { ApproverUserId = 10, Priority = 1 },
            new() { ApproverUserId = 10, Priority = 2 },
        };

        var act = async () => await _sut.CreatePolicyAsync(
            ApprovalDocumentType.PurchaseOrder,
            ApprovalRequesterType.User,
            1,
            null,
            approvers);

        await act.Should().ThrowAsync<Exception>().WithMessage("*Duplicate approvers*");
    }

    [Test]
    public async Task CreatePolicyAsync_MissingPriorityOne_Throws()
    {
        var approvers = new List<ApprovalPolicyApprover>
        {
            new() { ApproverUserId = 10, Priority = 2 },
        };

        var act = async () => await _sut.CreatePolicyAsync(
            ApprovalDocumentType.PurchaseOrder,
            ApprovalRequesterType.User,
            1,
            null,
            approvers);

        await act.Should().ThrowAsync<Exception>().WithMessage("*Priority 1 approver required*");
    }

    [Test]
    public async Task UpdatePolicyAsync_ReplacesApproversAndRules()
    {
        var policyId = await _sut.CreatePolicyAsync(
            ApprovalDocumentType.PurchaseOrder,
            ApprovalRequesterType.User,
            1,
            null,
            [new ApprovalPolicyApprover { ApproverUserId = 10, Priority = 1 }]);
        // Each call below simulates its own HTTP request against a fresh pooled DbContext, so clear
        // the shared test context between them rather than letting Add()'d entities stay tracked.
        _context.ChangeTracker.Clear();

        var updatedApprovers = new List<ApprovalPolicyApprover>
        {
            new() { ApproverUserId = 30, Priority = 1 },
            new() { ApproverUserId = 40, Priority = 2 },
        };

        await _sut.UpdatePolicyAsync(
            policyId,
            ApprovalDocumentType.Payments,
            ApprovalRequesterType.User,
            2,
            null,
            updatedApprovers);

        var saved = await _sut.GetByIdAsync(policyId);
        saved!.DocumentType.Should().Be(ApprovalDocumentType.Payments);
        saved.RequesterUserId.Should().Be(2);
        saved.Approvers.Select(a => a.ApproverUserId).Should().BeEquivalentTo([30, 40]);
    }

    [Test]
    public async Task DeletePolicyAsync_ExistingPolicy_RemovesFromDatabase()
    {
        var policyId = await _sut.CreatePolicyAsync(
            ApprovalDocumentType.PurchaseOrder,
            ApprovalRequesterType.User,
            1,
            null,
            [new ApprovalPolicyApprover { ApproverUserId = 10, Priority = 1 }]);
        _context.ChangeTracker.Clear();

        await _sut.DeletePolicyAsync(policyId);

        (await _sut.GetByIdAsync(policyId)).Should().BeNull();
        (await _sut.GetAllAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task SetActiveAsync_CanDeactivateAndReactivate_WithoutDeletingConfiguration()
    {
        var policyId = await _sut.CreatePolicyAsync(
            ApprovalDocumentType.PurchaseOrder,
            ApprovalRequesterType.User,
            1,
            null,
            [new ApprovalPolicyApprover { ApproverUserId = 10, Priority = 1 }]);
        _context.ChangeTracker.Clear();

        await _sut.SetActiveAsync(policyId, false);
        var deactivated = await _sut.GetByIdAsync(policyId);
        deactivated!.IsActive.Should().BeFalse();
        deactivated.Approvers.Should().HaveCount(1);
        _context.ChangeTracker.Clear();

        await _sut.SetActiveAsync(policyId, true);
        var reactivated = await _sut.GetByIdAsync(policyId);
        reactivated!.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task SetActiveAsync_ReactivatingIntoDuplicate_Throws()
    {
        var firstPolicyId = await _sut.CreatePolicyAsync(
            ApprovalDocumentType.PurchaseOrder,
            ApprovalRequesterType.User,
            1,
            null,
            [new ApprovalPolicyApprover { ApproverUserId = 10, Priority = 1 }]);
        _context.ChangeTracker.Clear();
        await _sut.SetActiveAsync(firstPolicyId, false);
        _context.ChangeTracker.Clear();

        // A second active policy now exists for the same requester + document type. Uses a distinct
        // approver instance — CreatePolicyAsync's Add() would otherwise try to re-insert the same
        // already-keyed ApprovalPolicyApprover object from the first policy above.
        await _sut.CreatePolicyAsync(
            ApprovalDocumentType.PurchaseOrder,
            ApprovalRequesterType.User,
            1,
            null,
            [new ApprovalPolicyApprover { ApproverUserId = 10, Priority = 1 }]);
        _context.ChangeTracker.Clear();

        var act = async () => await _sut.SetActiveAsync(firstPolicyId, true);

        await act.Should().ThrowAsync<Exception>().WithMessage("*active approval policy already exists*");
    }

    [Test]
    public async Task CreatePolicyAsync_GroupRequester_PersistsGroupPolicy()
    {
        _context.UserGroups.Add(new UserGroup
        {
            Id = 100,
            CompanyDb = SapCompanyDatabase.PBBPL_UAT.ToString(),
            Name = "Finance",
            IsActive = true,
            Members = [new UserGroupMember { UserId = 5 }],
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var policyId = await _sut.CreatePolicyAsync(
            ApprovalDocumentType.PurchaseOrder,
            ApprovalRequesterType.Group,
            null,
            100,
            [new ApprovalPolicyApprover { ApproverUserId = 10, Priority = 1 }]);

        var saved = await _sut.GetByIdAsync(policyId);
        saved!.RequesterType.Should().Be(ApprovalRequesterType.Group);
        saved.RequesterGroupId.Should().Be(100);
        saved.RequesterUserId.Should().BeNull();
    }

    [Test]
    public async Task SetActiveAsync_UnknownPolicy_Throws()
    {
        var act = async () => await _sut.SetActiveAsync(999, true);

        await act.Should().ThrowAsync<Exception>().WithMessage("*Policy not found*");
    }
}
