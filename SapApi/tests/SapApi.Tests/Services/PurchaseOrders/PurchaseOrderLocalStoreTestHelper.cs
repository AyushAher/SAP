using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Moq;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Caching;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Infrastructure.Persistence;

namespace SapApi.Tests.Services.PurchaseOrders;

internal static class PurchaseOrderLocalStoreTestHelper
{
    public static PurchaseOrderLocalStore Create(
        AppDbContext context,
        IHttpRequestHandler http,
        ICurrentCompanyDbAccessor companyDbAccessor) =>
        new(context, http, companyDbAccessor, CreateMasterDataService(http, companyDbAccessor));

    private static SapMasterDataService CreateMasterDataService(IHttpRequestHandler http, ICurrentCompanyDbAccessor companyDbAccessor)
    {
        var sapLogin = new Mock<ISapLoginService>();
        sapLogin.Setup(s => s.SapLoginAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var cache = new SapMasterDataCache(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

        return new SapMasterDataService(http, sapLogin.Object, cache, companyDbAccessor);
    }
}
