using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Infrastructure.Persistence;

namespace SapApi.Tests.Persistence;

[TestFixture]
public class SoftDeleteTests
{
    private AppDbContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
    }

    [TearDown]
    public async Task TearDown() => await _context.DisposeAsync();

    [Test]
    public async Task Remove_SetsIsDeleted_AndEntityExcludedFromDefaultQuery()
    {
        var payment = new StageWisePayment
        {
            CompanyDb = "TEST",
            Stage = StageWisePaymentStages.AgainstPoAcceptance,
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = DateTime.UtcNow
        };
        _context.StageWisePayments.Add(payment);
        await _context.SaveChangesAsync();

        _context.StageWisePayments.Remove(payment);
        await _context.SaveChangesAsync();

        var visible = await _context.StageWisePayments.ToListAsync();
        visible.Should().BeEmpty();

        var includingDeleted = await _context.StageWisePayments
            .IgnoreQueryFilters()
            .SingleAsync();
        includingDeleted.IsDeleted.Should().BeTrue();
    }
}
