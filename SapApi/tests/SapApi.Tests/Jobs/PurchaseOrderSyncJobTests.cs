using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Jobs;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Tests.Services.PurchaseOrders;
using SapApi.Shared.Configuration;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Jobs;

[TestFixture]
public class PurchaseOrderSyncJobTests
{
    private const string CompanyDb = "PBBPL_UAT";
    private const int RequestingUserId = 42;

    private AppDbContext _db = null!;
    private Mock<IHttpRequestHandler> _http = null!;
    private Mock<ISapLoginService> _sapLogin = null!;
    private HttpContextAccessor _httpContextAccessor = null!;
    private PurchaseOrderLocalStore _localStore = null!;

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
        _localStore = PurchaseOrderLocalStoreTestHelper.Create(_db, _http.Object, companyDb.Object);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task TryBeginFullSyncJob_RejectsSecondStartWhileRunning()
    {
        (await _localStore.TryBeginFullSyncJobAsync("job-1")).Should().BeTrue();
        (await _localStore.TryBeginFullSyncJobAsync("job-2")).Should().BeFalse();

        var status = await _localStore.GetSyncStateAsync();
        status!.Status.Should().Be(PurchaseOrderSyncState.StatusRunning);
        status.HangfireJobId.Should().Be("job-1");
    }

    [Test]
    public async Task ExecuteAsync_ReusesCachedSession_WithoutServicePassword()
    {
        await _localStore.TryBeginFullSyncJobAsync(null);

        _http.Setup(h => h.GetOrThrowAsync<GetAllSapPurchaseOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAllSapPurchaseOrdersResponse { Value = [] });

        // SapLogin succeeds via cached session — no service password needed.
        var job = new PurchaseOrderSyncJob(
            _httpContextAccessor,
            _sapLogin.Object,
            _localStore,
            Options.Create(new SapCredentials { Accounts = [] }),
            Options.Create(new HangfireOptions { ServiceUserId = 0 }));

        await job.ExecuteAsync(CompanyDb, RequestingUserId, performContext: null);

        _sapLogin.Verify(s => s.SapLoginAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _sapLogin.Verify(s => s.LoginWithUserCredentialsAsync(
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SapCompanyDatabase>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _httpContextAccessor.HttpContext.Should().BeNull();

        var status = await _localStore.GetSyncStateAsync();
        status!.Status.Should().Be(PurchaseOrderSyncState.StatusSucceeded);
    }

    [Test]
    public async Task ExecuteAsync_FallsBackToServicePassword_WhenNoCachedSession()
    {
        await _localStore.TryBeginFullSyncJobAsync(null);

        _sapLogin.Setup(s => s.SapLoginAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new ApiErrorException("SYS-01", "SAP session expired")));
        _sapLogin.Setup(s => s.LoginWithUserCredentialsAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SapCompanyDatabase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _http.Setup(h => h.GetOrThrowAsync<GetAllSapPurchaseOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAllSapPurchaseOrdersResponse { Value = [] });

        // After LoginWithUserCredentials, batch loop SapLoginAsync must succeed.
        var loginCalls = 0;
        _sapLogin.Setup(s => s.SapLoginAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                loginCalls++;
                return loginCalls == 1
                    ? Task.FromException(new ApiErrorException("SYS-01", "SAP session expired"))
                    : Task.CompletedTask;
            });

        var job = CreateJob();
        await job.ExecuteAsync(CompanyDb, RequestingUserId, performContext: null);

        _sapLogin.Verify(s => s.LoginWithUserCredentialsAsync(
            RequestingUserId, "manager", "secret", SapCompanyDatabase.PBBPL_UAT, It.IsAny<CancellationToken>()), Times.Once);

        var status = await _localStore.GetSyncStateAsync();
        status!.Status.Should().Be(PurchaseOrderSyncState.StatusSucceeded);
    }

    [Test]
    public async Task ExecuteAsync_MultipleBatches_CallsSapLoginEachBatch()
    {
        await _localStore.TryBeginFullSyncJobAsync(null);

        var docEntries = Enumerable.Range(1, 401).ToArray();
        _http.Setup(h => h.GetOrThrowAsync<GetAllSapPurchaseOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) =>
            {
                var cursor = 0;
                var marker = "DocEntry gt ";
                var index = url.IndexOf(marker, StringComparison.Ordinal);
                if (index >= 0)
                    cursor = int.Parse(new string(url[(index + marker.Length)..].TakeWhile(char.IsDigit).ToArray()));

                return new GetAllSapPurchaseOrdersResponse
                {
                    Value = docEntries
                        .Where(d => d > cursor)
                        .Take(500)
                        .Select(d => new SapPurchaseOrdersResponse { DocEntry = d })
                        .ToList(),
                };
            });

        _http.Setup(h => h.GetOrThrowAsync<SapPurchaseOrdersResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) =>
            {
                var id = int.Parse(url.Split('(').Last().TrimEnd(')'));
                return new SapPurchaseOrdersResponse
                {
                    DocEntry = id,
                    DocNum = id,
                    CardCode = "V1",
                };
            });

        var job = CreateJob();
        await job.ExecuteAsync(CompanyDb, RequestingUserId, performContext: null);

        _sapLogin.Verify(s => s.SapLoginAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));

        var status = await _localStore.GetSyncStateAsync();
        status!.Status.Should().Be(PurchaseOrderSyncState.StatusSucceeded);
        status.UpsertedCount.Should().Be(401);
    }

    [Test]
    public async Task ExecuteAsync_NoSessionAndNoPassword_MarksFailed()
    {
        _sapLogin.Setup(s => s.SapLoginAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new ApiErrorException("SYS-01", "SAP session expired")));

        var job = new PurchaseOrderSyncJob(
            _httpContextAccessor,
            _sapLogin.Object,
            _localStore,
            Options.Create(new SapCredentials { Accounts = [] }),
            Options.Create(new HangfireOptions { ServiceUserId = 0 }));

        await _localStore.TryBeginFullSyncJobAsync(null);

        var act = async () => await job.ExecuteAsync(CompanyDb, RequestingUserId, null);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No SAP session*");

        var status = await _localStore.GetSyncStateAsync();
        status!.Status.Should().Be(PurchaseOrderSyncState.StatusFailed);
    }

    [Test]
    public void ResolveServiceLogin_FallsBackWhenCompanyNotListed()
    {
        var (user, password) = MasterDataCacheRefreshJob.ResolveServiceLogin(
            [
                new SapCompanyCredential
                {
                    Username = "manager",
                    Password = "from-uat",
                    CompanyDb = "PBBPL_UAT",
                },
            ],
            "PBBPL_LIVE");

        user.Should().Be("manager");
        password.Should().Be("from-uat");
    }

    private PurchaseOrderSyncJob CreateJob() =>
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
