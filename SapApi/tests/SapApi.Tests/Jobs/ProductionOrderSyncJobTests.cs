using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Jobs;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services.ProductionOrders;
using SapApi.Shared;
using SapApi.Shared.Configuration;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Responses.Sap;
using SapApi.Tests.Services.ProductionOrders;

namespace SapApi.Tests.Jobs;

[TestFixture]
public class ProductionOrderSyncJobTests
{
    private const string CompanyDb = "PBBPL_UAT";
    private const int RequestingUserId = 42;

    private AppDbContext _db = null!;
    private Mock<IHttpRequestHandler> _http = null!;
    private Mock<ISapLoginService> _sapLogin = null!;
    private HttpContextAccessor _httpContextAccessor = null!;
    private ProductionOrderLocalStore _localStore = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _http = new Mock<IHttpRequestHandler>();
        _sapLogin = new Mock<ISapLoginService>();
        _sapLogin.Setup(s => s.SapLoginAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var companyDb = new Mock<ICurrentCompanyDbAccessor>();
        companyDb.Setup(c => c.GetCompanyDbName()).Returns(CompanyDb);

        _httpContextAccessor = new HttpContextAccessor();
        _localStore = ProductionOrderLocalStoreTestHelper.Create(
            _db,
            _http.Object,
            companyDb.Object,
            _httpContextAccessor);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private void SetupEmptySap() =>
        _http.Setup(h => h.GetOrThrowAsync<GetAllSapProductionOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAllSapProductionOrdersResponse { Value = [] });

    [Test]
    public async Task TryBeginFullSyncJob_rejects_a_second_start_while_running()
    {
        (await _localStore.TryBeginFullSyncJobAsync("job-1")).Should().BeTrue();
        (await _localStore.TryBeginFullSyncJobAsync("job-2")).Should().BeFalse();

        var status = await _localStore.GetSyncStateAsync();
        status!.Status.Should().Be(ProductionOrderSyncState.StatusRunning);
        status.HangfireJobId.Should().Be("job-1");
    }

    [Test]
    public async Task ExecuteAsync_reuses_the_cached_session_without_a_service_password()
    {
        await _localStore.TryBeginFullSyncJobAsync(null);
        SetupEmptySap();

        var job = new ProductionOrderSyncJob(
            _httpContextAccessor,
            _sapLogin.Object,
            _localStore,
            Options.Create(new SapCredentials { Accounts = [] }),
            Options.Create(new HangfireOptions { ServiceUserId = 0 }));

        await job.ExecuteAsync(CompanyDb, RequestingUserId, performContext: null);

        _sapLogin.Verify(s => s.SapLoginAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _sapLogin.Verify(
            s => s.LoginWithUserCredentialsAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SapCompanyDatabase>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _httpContextAccessor.HttpContext.Should().BeNull();

        var status = await _localStore.GetSyncStateAsync();
        status!.Status.Should().Be(ProductionOrderSyncState.StatusSucceeded);
    }

    [Test]
    public async Task ExecuteAsync_falls_back_to_the_service_password_when_no_session_is_cached()
    {
        await _localStore.TryBeginFullSyncJobAsync(null);
        SetupEmptySap();

        _sapLogin.Setup(s => s.LoginWithUserCredentialsAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SapCompanyDatabase>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Only the first probe fails; the per-batch renewal must then succeed.
        var loginCalls = 0;
        _sapLogin.Setup(s => s.SapLoginAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                loginCalls++;
                return loginCalls == 1
                    ? Task.FromException(new ApiErrorException("SYS-01", "SAP session expired"))
                    : Task.CompletedTask;
            });

        await CreateJob().ExecuteAsync(CompanyDb, RequestingUserId, performContext: null);

        _sapLogin.Verify(
            s => s.LoginWithUserCredentialsAsync(
                RequestingUserId, "manager", "secret", SapCompanyDatabase.PBBPL_UAT, It.IsAny<CancellationToken>()),
            Times.Once);

        var status = await _localStore.GetSyncStateAsync();
        status!.Status.Should().Be(ProductionOrderSyncState.StatusSucceeded);
    }

    [Test]
    public async Task ExecuteAsync_marks_the_sync_failed_when_there_is_no_session_and_no_password()
    {
        _sapLogin.Setup(s => s.SapLoginAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new ApiErrorException("SYS-01", "SAP session expired")));

        var job = new ProductionOrderSyncJob(
            _httpContextAccessor,
            _sapLogin.Object,
            _localStore,
            Options.Create(new SapCredentials { Accounts = [] }),
            Options.Create(new HangfireOptions { ServiceUserId = 0 }));

        await _localStore.TryBeginFullSyncJobAsync(null);

        var act = async () => await job.ExecuteAsync(CompanyDb, RequestingUserId, null);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No SAP session*");

        var status = await _localStore.GetSyncStateAsync();
        status!.Status.Should().Be(ProductionOrderSyncState.StatusFailed);
    }

    [Test]
    public async Task ExecuteAsync_renews_the_session_between_batches_and_imports_every_order()
    {
        await _localStore.TryBeginFullSyncJobAsync(null);

        var entries = Enumerable.Range(1, 401).ToArray();
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
                    Value = entries
                        .Where(e => e > cursor)
                        .Select(e => new SapProductionOrdersResponse { AbsoluteEntry = e })
                        .ToList(),
                };
            });

        _http.Setup(h => h.GetOrThrowAsync<SapProductionOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) =>
            {
                var id = int.Parse(url.Split('(').Last().TrimEnd(')'));
                return new SapProductionOrdersResponse
                {
                    AbsoluteEntry = id,
                    DocumentNumber = id,
                    Status = Constants.SapProductionOrderStatus.Closed,
                };
            });

        await CreateJob().ExecuteAsync(CompanyDb, RequestingUserId, performContext: null);

        _sapLogin.Verify(s => s.SapLoginAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
        _db.ProductionOrders.Count().Should().Be(401);

        var status = await _localStore.GetSyncStateAsync();
        status!.Status.Should().Be(ProductionOrderSyncState.StatusSucceeded);
        status.UpsertedCount.Should().Be(401);
    }

    private ProductionOrderSyncJob CreateJob() =>
        new(
            _httpContextAccessor,
            _sapLogin.Object,
            _localStore,
            Options.Create(new SapCredentials
            {
                Accounts =
                [
                    new SapCompanyCredential
                    {
                        Username = "manager",
                        Password = "secret",
                        CompanyDb = CompanyDb,
                    },
                ],
            }),
            Options.Create(new HangfireOptions { ServiceUserId = 0 }));
}
