using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Caching;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Enums;
using SapApi.Shared.Models;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services.Sap;

[TestFixture]
public class SapMasterDataServiceTests
{
    private Mock<IHttpRequestHandler> _http = null!;
    private Mock<ISapLoginService> _sapLogin = null!;
    private Mock<ICurrentCompanyDbAccessor> _companyDb = null!;
    private ISapMasterDataCache _cache = null!;
    private SapMasterDataService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _http = new Mock<IHttpRequestHandler>();
        _sapLogin = new Mock<ISapLoginService>();
        _companyDb = new Mock<ICurrentCompanyDbAccessor>();
        _companyDb.Setup(c => c.GetCompanyDbName()).Returns(SapCompanyDatabase.PBBPL_UAT.ToString());
        _cache = new SapMasterDataCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        _sut = new SapMasterDataService(_http.Object, _sapLogin.Object, _cache, _companyDb.Object);
    }

    [Test]
    public async Task GetItemByCodeAsync_CalledTwiceWithSameCode_OnlyHitsSapOnce()
    {
        _http
            .Setup(h => h.GetAsync<SapItemsResponse>(It.IsAny<string>(), true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapItemsResponse { Value = [new ItemsResponse { ItemCode = "ITM-1", ItemName = "Widget" }] });

        var first = await _sut.GetItemByCodeAsync("ITM-1");
        var second = await _sut.GetItemByCodeAsync("ITM-1");

        first!.ItemName.Should().Be("Widget");
        second!.ItemName.Should().Be("Widget");
        _http.Verify(h => h.GetAsync<SapItemsResponse>(It.IsAny<string>(), true, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetItemByCodeAsync_DifferentCodes_HitsSapForEachDistinctCode()
    {
        _http
            .Setup(h => h.GetAsync<SapItemsResponse>(It.IsAny<string>(), true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapItemsResponse { Value = [new ItemsResponse { ItemCode = "ITM-X" }] });

        await _sut.GetItemByCodeAsync("ITM-1");
        await _sut.GetItemByCodeAsync("ITM-2");

        _http.Verify(h => h.GetAsync<SapItemsResponse>(It.IsAny<string>(), true, true, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task GetItemByCodeAsync_RequestingFewerFields_OnlySelectsRequestedFieldsPlusKey()
    {
        string? capturedUrl = null;
        _http
            .Setup(h => h.GetAsync<SapItemsResponse>(It.IsAny<string>(), true, true, It.IsAny<CancellationToken>()))
            .Callback<string, bool, bool, CancellationToken>((url, _, _, _) => capturedUrl = url)
            .ReturnsAsync(new SapItemsResponse { Value = [new ItemsResponse { ItemCode = "ITM-1" }] });

        await _sut.GetItemByCodeAsync("ITM-1", fields: ["ItemName"]);

        capturedUrl.Should().NotBeNull();
        capturedUrl!.Should().Contain("ItemCode").And.Contain("ItemName");
        capturedUrl.Should().NotContain("InventoryWeight");
    }

    [Test]
    public async Task SearchItemsAsync_CalledTwiceWithSameRequest_OnlyHitsSapOnce()
    {
        _http
            .Setup(h => h.GetAsync<SapItemsResponse>(It.IsAny<string>(), true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapItemsResponse { Value = [new ItemsResponse { ItemCode = "ITM-1" }] });

        var request = new PaginationRequest { PageNumber = 1, PageSize = 20 };
        await _sut.SearchItemsAsync(request);
        await _sut.SearchItemsAsync(new PaginationRequest { PageNumber = 1, PageSize = 20 });

        _http.Verify(h => h.GetAsync<SapItemsResponse>(It.IsAny<string>(), true, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task SearchSalesOrdersAsync_NeverCached_AlwaysHitsSap()
    {
        _http
            .Setup(h => h.GetAsync<GetAllSapSalesOrderResponse>(It.IsAny<string>(), true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAllSapSalesOrderResponse { Value = [] });

        var request = new PaginationRequest { PageNumber = 1, PageSize = 20 };
        await _sut.SearchSalesOrdersAsync(request);
        await _sut.SearchSalesOrdersAsync(new PaginationRequest { PageNumber = 1, PageSize = 20 });

        _http.Verify(h => h.GetAsync<GetAllSapSalesOrderResponse>(It.IsAny<string>(), true, true, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task GetBusinessPartnerByCardCodeAsync_DifferentCompanyDbs_DoNotShareCache()
    {
        _companyDb.Setup(c => c.GetCompanyDbName()).Returns(SapCompanyDatabase.PBBPL_UAT.ToString());
        _http
            .Setup(h => h.GetAsync<SapBusinessPartnerResponse>(It.IsAny<string>(), true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapBusinessPartnerResponse { Value = [new SapBusinessPartner { CardCode = "V001" }] });

        await _sut.GetBusinessPartnerByCardCodeAsync("V001");

        _companyDb.Setup(c => c.GetCompanyDbName()).Returns(SapCompanyDatabase.PBBPL_LIVE.ToString());
        await _sut.GetBusinessPartnerByCardCodeAsync("V001");

        _http.Verify(h => h.GetAsync<SapBusinessPartnerResponse>(It.IsAny<string>(), true, true, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>
    /// SAP names this function import IndiaSacCodeService_GetList; IndiaSacService_GetList answers
    /// "Service Not Found", and the lookup swallows that into an empty SAC list, so the picker just
    /// looks empty. Pin the endpoint.
    /// </summary>
    [Test]
    public async Task SearchSacCodesAsync_PostsToTheIndiaSacCodeService()
    {
        string? capturedUrl = null;
        _http
            .Setup(h => h.PostAsync<object, IndiaSacListEnvelope>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?, CancellationToken>((url, _, _) => capturedUrl = url)
            .ReturnsAsync(new IndiaSacListEnvelope
            {
                Value = [new IndiaSacCodeResponse { AbsEntry = 11, ServiceCode = "998335" }],
            });

        var result = await _sut.SearchSacCodesAsync(new PaginationRequest { PageNumber = 1, PageSize = 20 });

        capturedUrl.Should().EndWith("/IndiaSacCodeService_GetList");
        result.Data.Should().HaveCount(1);
        result.Data![0].DisplayLabel.Should().Be("998335");
    }

    [Test]
    public async Task SearchSacCodesAsync_MatchesOnPartOfTheServiceCode()
    {
        _http
            .Setup(h => h.PostAsync<object, IndiaSacListEnvelope>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndiaSacListEnvelope
            {
                Value =
                [
                    new IndiaSacCodeResponse { AbsEntry = 11, ServiceCode = "998335" },
                    new IndiaSacCodeResponse { AbsEntry = 12, ServiceCode = "997156" },
                ],
            });

        var result = await _sut.SearchSacCodesAsync(new PaginationRequest
        {
            PageNumber = 1,
            PageSize = 20,
            Filters = [new FilterModel { Field = "__search", Value = "8335" }],
        });

        result.Data.Should().HaveCount(1);
        result.Data![0].AbsEntry.Should().Be(11);
    }

    [Test]
    public async Task SearchSacCodesAsync_SapFailure_ReturnsEmptyListInsteadOfThrowing()
    {
        _http
            .Setup(h => h.PostAsync<object, IndiaSacListEnvelope>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service Not Found"));

        var result = await _sut.SearchSacCodesAsync(new PaginationRequest { PageNumber = 1, PageSize = 20 });

        result.Data.Should().BeEmpty();
    }
}
