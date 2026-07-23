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
public class UserGroupServiceTests
{
    private AppDbContext _context = null!;
    private UserGroupService _sut = null!;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        foreach (var id in new[] { 1, 2, 3 })
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
        companyDbAccessor.Setup(x => x.GetCompanyDbName()).Returns(SapCompanyDatabase.PBBPL_UAT.ToString());
        _sut = new UserGroupService(_context, companyDbAccessor.Object);
    }

    [TearDown]
    public async Task TearDown() => await _context.DisposeAsync();

    [Test]
    public async Task CreateAsync_WithMembers_PersistsGroup()
    {
        var id = await _sut.CreateAsync("Finance", "AP team", [1, 2]);

        var saved = await _sut.GetByIdAsync(id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Finance");
        saved.Members.Select(m => m.UserId).Should().BeEquivalentTo([1, 2]);
    }

    [Test]
    public async Task UpdateAsync_ReplacesMembers()
    {
        var id = await _sut.CreateAsync("Finance", null, [1]);
        _context.ChangeTracker.Clear();

        await _sut.UpdateAsync(id, "Finance Updated", "desc", [2, 3]);

        var saved = await _sut.GetByIdAsync(id);
        saved!.Name.Should().Be("Finance Updated");
        saved.Members.Select(m => m.UserId).Should().BeEquivalentTo([2, 3]);
    }

    [Test]
    public async Task DeleteAsync_UsedByPolicy_Throws()
    {
        var id = await _sut.CreateAsync("Finance", null, [1]);
        _context.ChangeTracker.Clear();

        _context.ApprovalPolicies.Add(new ApprovalPolicy
        {
            CompanyDb = SapCompanyDatabase.PBBPL_UAT.ToString(),
            DocumentType = ApprovalDocumentType.PurchaseOrder,
            RequesterType = ApprovalRequesterType.Group,
            RequesterGroupId = id,
            IsActive = true,
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var act = async () => await _sut.DeleteAsync(id);
        await act.Should().ThrowAsync<Exception>().WithMessage("*used by one or more approval policies*");
    }

    [Test]
    public async Task CreateAsync_DuplicateName_Throws()
    {
        await _sut.CreateAsync("Finance", null, []);
        _context.ChangeTracker.Clear();

        var act = async () => await _sut.CreateAsync("Finance", null, []);
        await act.Should().ThrowAsync<Exception>().WithMessage("*already exists*");
    }

    [Test]
    public async Task CreateAsync_UserAlreadyInAnotherGroup_Throws()
    {
        await _sut.CreateAsync("Finance", null, [1]);
        _context.ChangeTracker.Clear();

        var act = async () => await _sut.CreateAsync("Ops", null, [1, 2]);
        await act.Should().ThrowAsync<Exception>().WithMessage("*only one group*");
    }

    [Test]
    public async Task UpdateAsync_UserAlreadyInAnotherGroup_Throws()
    {
        await _sut.CreateAsync("Finance", null, [1]);
        var opsId = await _sut.CreateAsync("Ops", null, [2]);
        _context.ChangeTracker.Clear();

        var act = async () => await _sut.UpdateAsync(opsId, "Ops", null, [1, 2]);
        await act.Should().ThrowAsync<Exception>().WithMessage("*only one group*");
    }

    [Test]
    public async Task UpdateAsync_KeepingExistingMembers_Succeeds()
    {
        var id = await _sut.CreateAsync("Finance", null, [1, 2]);
        _context.ChangeTracker.Clear();

        await _sut.UpdateAsync(id, "Finance", null, [1, 2, 3]);

        var saved = await _sut.GetByIdAsync(id);
        saved!.Members.Select(m => m.UserId).Should().BeEquivalentTo([1, 2, 3]);
    }
}
