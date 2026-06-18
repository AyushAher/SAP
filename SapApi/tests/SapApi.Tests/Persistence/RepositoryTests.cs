using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Infrastructure.Persistence;

namespace SapApi.Tests.Persistence;

[TestFixture]
public class RepositoryTests
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
    public async Task Repository_AddAndFind_WorksWithTransaction()
    {
        var uow = new UnitOfWork(_context);
        var repo = uow.Repository<StageWisePayment>();

        await repo.AddAsync(new StageWisePayment
        {
            Stage = StageWisePaymentStages.AgainstPoAcceptance,
            CreatedOn = DateTime.UtcNow,
            LastModifiedOn = DateTime.UtcNow
        });
        await uow.SaveChangesAsync();

        var all = await repo.GetAllAsync();
        all.Should().HaveCount(1);
    }
}
