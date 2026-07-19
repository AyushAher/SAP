using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Infrastructure.Persistence;

namespace SapApi.Tests.Persistence;

/// <summary>
/// Regression coverage for the bug reported in production: ApprovalExecutionService.ExecuteAsync and
/// FinalizeApprovalAsync each independently re-query and mark Modified the same StageWisePayment row
/// within a single HTTP request/DbContext. Because the app's DbContext pool uses
/// QueryTrackingBehavior.NoTracking, each query returns a distinct object instance, so the second
/// DbSet.Update(...)/Entry(...).State=Modified on that same row's key threw:
/// "The instance of entity type '...' cannot be tracked because another instance with the same key
/// value ... is already being tracked." AttachModified must merge into the already-tracked instance
/// instead of attaching a duplicate.
/// </summary>
[TestFixture]
public class EntityTrackingExtensionsTests
{
    private AppDbContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
    }

    [TearDown]
    public async Task TearDown() => await _context.DisposeAsync();

    [Test]
    public async Task AttachModified_EntityNotYetTracked_MarksItModifiedAndPersists()
    {
        var approval = new UserApproval { UserId = 1, ApprovalRequestId = 1, Priority = 1 };
        _context.UserApprovals.Add(approval);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var fetched = await _context.UserApprovals.FirstAsync(x => x.Id == approval.Id);
        fetched.Comment = "first update";

        _context.AttachModified(fetched);
        await _context.SaveChangesAsync();

        var persisted = await _context.UserApprovals.AsNoTracking().FirstAsync(x => x.Id == approval.Id);
        persisted.Comment.Should().Be("first update");
    }

    [Test]
    public async Task AttachModified_SecondIndependentlyFetchedInstanceOfSameRow_MergesInsteadOfThrowing()
    {
        var approval = new UserApproval { UserId = 1, ApprovalRequestId = 1, Priority = 1 };
        _context.UserApprovals.Add(approval);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Simulates ExecuteAsync: fetches the row fresh and marks it Modified — this stays tracked.
        var firstFetch = await _context.UserApprovals.FirstAsync(x => x.Id == approval.Id);
        firstFetch.Comment = "from first step";
        _context.AttachModified(firstFetch);

        // Simulates FinalizeApprovalAsync running later in the same request/DbContext: fetches the
        // SAME row again — NoTracking means this is a genuinely different object instance — and also
        // needs to mark it Modified. Plain DbSet.Update/Entry(...).State would throw here.
        var secondFetch = await _context.UserApprovals.FirstAsync(x => x.Id == approval.Id);
        secondFetch.Comment = "from second step";

        var act = () => _context.AttachModified(secondFetch);
        act.Should().NotThrow();

        await _context.SaveChangesAsync();

        var persisted = await _context.UserApprovals.AsNoTracking().FirstAsync(x => x.Id == approval.Id);
        persisted.Comment.Should().Be("from second step");
    }
}
