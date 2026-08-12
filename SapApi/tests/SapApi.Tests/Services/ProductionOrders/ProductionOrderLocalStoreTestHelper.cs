using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Caching;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services.ProductionOrders;
using SapApi.Infrastructure.Services.Sap;

namespace SapApi.Tests.Services.ProductionOrders;

internal static class ProductionOrderLocalStoreTestHelper
{
    public static ProductionOrderLocalStore Create(
        AppDbContext context,
        IHttpRequestHandler http,
        ICurrentCompanyDbAccessor companyDbAccessor,
        IHttpContextAccessor? httpContextAccessor = null) =>
        new(
            context,
            http,
            companyDbAccessor,
            CreateMasterDataService(http, companyDbAccessor),
            httpContextAccessor ?? new HttpContextAccessor());

    private static SapMasterDataService CreateMasterDataService(
        IHttpRequestHandler http,
        ICurrentCompanyDbAccessor companyDbAccessor)
    {
        var sapLogin = new Mock<ISapLoginService>();
        sapLogin.Setup(s => s.SapLoginAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var cache = new SapMasterDataCache(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

        return new SapMasterDataService(http, sapLogin.Object, cache, companyDbAccessor);
    }
}
