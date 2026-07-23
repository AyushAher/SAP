using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Jobs;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Configuration;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Jobs;

[TestFixture]
public class MasterDataCacheRefreshJobTests
{
    [Test]
    public void ResolveCompanyDb_ParsesConfiguredValue()
    {
        MasterDataCacheRefreshJob.ResolveCompanyDb("PBBPL_LIVE").Should().Be(SapCompanyDatabase.PBBPL_LIVE);
        MasterDataCacheRefreshJob.ResolveCompanyDb("pbbpl_uat").Should().Be(SapCompanyDatabase.PBBPL_UAT);
    }

    [Test]
    public void ResolveCompanyDb_UnknownValue_Throws()
    {
        var act = () => MasterDataCacheRefreshJob.ResolveCompanyDb("DOES_NOT_EXIST");
        act.Should().Throw<InvalidOperationException>().WithMessage("*Unknown SapCredentials CompanyDb*");
    }

    [Test]
    public void ResolvePassword_PrefersAccountPasswordOverEnv()
    {
        var password = MasterDataCacheRefreshJob.ResolvePassword(new SapCompanyCredential
        {
            Password = "from-config",
            CompanyDb = "PBBPL_UAT",
        });
        password.Should().Be("from-config");
    }

    [Test]
    public void CreateServiceHttpContext_SetsUserIdAndCompanyDbClaims()
    {
        var ctx = MasterDataCacheRefreshJob.CreateServiceHttpContext(0, SapCompanyDatabase.PBBPL_UAT);

        ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value.Should().Be("0");
        ctx.User.FindFirst(SapApi.Shared.SapClaimTypes.CompanyDb)!.Value.Should().Be("PBBPL_UAT");
    }

    [Test]
    public async Task ExecuteAsync_EmptyAccounts_ThrowsBeforeCallingSap()
    {
        var sapLogin = new Mock<ISapLoginService>();
        var job = CreateJob(sapLogin.Object, new SapCredentials { Accounts = [] });

        var act = async () => await job.ExecuteAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SapCredentials:Accounts is empty*");
        sapLogin.Verify(s => s.SapLoginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_MissingPassword_ThrowsBeforeCallingSap()
    {
        var sapLogin = new Mock<ISapLoginService>();
        var job = CreateJob(sapLogin.Object, new SapCredentials
        {
            Accounts =
            [
                new SapCompanyCredential { Username = "manager", CompanyDb = "PBBPL_UAT", Password = null },
            ],
        });

        var previousPassword = Environment.GetEnvironmentVariable("SAP_PASSWORD");
        var previousDev = Environment.GetEnvironmentVariable("SAP_DEV_PASSWORD");
        var previousPerCompany = Environment.GetEnvironmentVariable("SAP_PASSWORD_PBBPL_UAT");
        Environment.SetEnvironmentVariable("SAP_PASSWORD", null);
        Environment.SetEnvironmentVariable("SAP_DEV_PASSWORD", null);
        Environment.SetEnvironmentVariable("SAP_PASSWORD_PBBPL_UAT", null);
        try
        {
            var act = async () => await job.ExecuteAsync();
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*password is required*");
            sapLogin.Verify(s => s.SapLoginAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SAP_PASSWORD", previousPassword);
            Environment.SetEnvironmentVariable("SAP_DEV_PASSWORD", previousDev);
            Environment.SetEnvironmentVariable("SAP_PASSWORD_PBBPL_UAT", previousPerCompany);
        }
    }

    [Test]
    public async Task ExecuteAsync_WithMultipleAccounts_RefreshesEachCompanyDb()
    {
        var http = new HttpContextAccessor();
        var sapLogin = new Mock<ISapLoginService>();
        var establishedCompanies = new HashSet<SapCompanyDatabase>();
        sapLogin
            .Setup(s => s.SapLoginAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var claim = http.HttpContext?.User.FindFirst(SapApi.Shared.SapClaimTypes.CompanyDb)?.Value;
                if (Enum.TryParse<SapCompanyDatabase>(claim, out var db) && establishedCompanies.Contains(db))
                    return Task.CompletedTask;
                return Task.FromException(new ApiErrorException("SYS-01", "no session"));
            });
        sapLogin
            .Setup(s => s.LoginWithUserCredentialsAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SapCompanyDatabase>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, string, SapCompanyDatabase, CancellationToken>((_, _, _, db, _) => establishedCompanies.Add(db))
            .Returns(Task.CompletedTask);

        var masterHttp = new Mock<IHttpRequestHandler>();
        SetupEmptyMasterResponses(masterHttp);

        var companyDb = new Mock<ICurrentCompanyDbAccessor>();
        companyDb.Setup(c => c.GetCompanyDbName()).Returns(() =>
            http.HttpContext?.User.FindFirst(SapApi.Shared.SapClaimTypes.CompanyDb)?.Value ?? "PBBPL_UAT");

        var masterData = new SapMasterDataService(
            masterHttp.Object,
            sapLogin.Object,
            new PassthroughMasterDataCache(),
            companyDb.Object);

        var job = new MasterDataCacheRefreshJob(
            http,
            sapLogin.Object,
            masterData,
            Options.Create(new SapCredentials
            {
                Accounts =
                [
                    new SapCompanyCredential { Username = "manager", Password = "uat-secret", CompanyDb = "PBBPL_UAT" },
                    new SapCompanyCredential { Username = "manager", Password = "live-secret", CompanyDb = "PBBPL_LIVE" },
                ],
            }),
            Options.Create(new HangfireOptions { ServiceUserId = 0 }));

        await job.ExecuteAsync();

        sapLogin.Verify(s => s.LoginWithUserCredentialsAsync(
            0, "manager", "uat-secret", SapCompanyDatabase.PBBPL_UAT, It.IsAny<CancellationToken>()), Times.Once);
        sapLogin.Verify(s => s.LoginWithUserCredentialsAsync(
            0, "manager", "live-secret", SapCompanyDatabase.PBBPL_LIVE, It.IsAny<CancellationToken>()), Times.Once);
        http.HttpContext.Should().BeNull("synthetic HttpContext must be cleared after the job finishes");
    }

    private static MasterDataCacheRefreshJob CreateJob(ISapLoginService sapLogin, SapCredentials credentials)
    {
        var masterHttp = new Mock<IHttpRequestHandler>();
        var companyDb = new Mock<ICurrentCompanyDbAccessor>();
        companyDb.Setup(c => c.GetCompanyDbName()).Returns("PBBPL_UAT");
        var masterData = new SapMasterDataService(
            masterHttp.Object,
            sapLogin,
            new PassthroughMasterDataCache(),
            companyDb.Object);

        return new MasterDataCacheRefreshJob(
            new HttpContextAccessor(),
            sapLogin,
            masterData,
            Options.Create(credentials),
            Options.Create(new HangfireOptions { ServiceUserId = 0 }));
    }

    private static void SetupEmptyMasterResponses(Mock<IHttpRequestHandler> http)
    {
        http.Setup(h => h.GetAsync<SapItemsResponse>(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapItemsResponse { Value = [] });
        http.Setup(h => h.GetAsync<SapWarehousesResponse>(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapWarehousesResponse { Value = [] });
        http.Setup(h => h.GetAsync<GetAllSapTaxCodesResponse>(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAllSapTaxCodesResponse { Value = [] });
        http.Setup(h => h.GetAsync<SapGetAllProjectDetailsResponse>(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapGetAllProjectDetailsResponse { Value = [] });
        http.Setup(h => h.GetAsync<SapBusinessPartnerResponse>(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapBusinessPartnerResponse { Value = [] });
        http.Setup(h => h.GetAsync<SapGetAllBranchesResponse>(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapGetAllBranchesResponse { Value = [] });
    }

    private sealed class PassthroughMasterDataCache : ISapMasterDataCache
    {
        public Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, TimeSpan ttl, CancellationToken cancellationToken = default) =>
            factory();
    }
}
