using Moq;
using SapApi.Domain.Interfaces;
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
        new(context, http, companyDbAccessor, CreateMasterDataService(companyDbAccessor));

    private static SapMasterDataService CreateMasterDataService(ICurrentCompanyDbAccessor companyDbAccessor)
    {
        var http = new Mock<IHttpRequestHandler>();
        var sapLogin = new Mock<ISapLoginService>();
        sapLogin.Setup(s => s.SapLoginAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var cache = new Mock<ISapMasterDataCache>();
        cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<object?>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<object?>> factory, TimeSpan _, CancellationToken ct) => factory()!);

        return new SapMasterDataService(http.Object, sapLogin.Object, cache.Object, companyDbAccessor);
    }
}
