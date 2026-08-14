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
    /// The IndiaSacCodeService_GetList function import returns AbsEntry + ServiceCode only, so the
    /// picker reads the IndiaSacCode entity set instead. The exact resource name matters: the plural
    /// "IndiaSacCodes" answers "Unrecognized resource path", and the lookup swallows that into an
    /// empty list, so a wrong path just looks like a company with no SAC codes. Pin the path.
    /// </summary>
    [Test]
    public async Task SearchSacCodesAsync_ReadsTheIndiaSacCodeEntitySet_AndSurfacesServiceName()
    {
        string? capturedUrl = null;
        SetupSacPages(
            urls => capturedUrl = urls,
            [
                new IndiaSacCodeResponse
                {
                    AbsEntry = 11,
                    ServiceCode = "998335",
                    ServiceName = "ENGINEERING SERVICES FOR MANUFACTURING PROJECTS",
                },
            ]);

        var result = await _sut.SearchSacCodesAsync(new PaginationRequest { PageNumber = 1, PageSize = 20 });

        capturedUrl.Should().Contain("/IndiaSacCode?$select=");
        capturedUrl.Should().NotContain("IndiaSacCodes");
        capturedUrl.Should().NotContain("_GetList");
        result.Data.Should().HaveCount(1);
        result.Data![0].DisplayLabel.Should().Be("998335 - ENGINEERING SERVICES FOR MANUFACTURING PROJECTS");
        result.Data[0].ServiceName.Should().Be("ENGINEERING SERVICES FOR MANUFACTURING PROJECTS");
    }

    [Test]
    public async Task SearchHsnCodesAsync_ReadsTheIndiaHsnEntitySet_AndSurfacesDescription()
    {
        string? capturedUrl = null;
        SetupHsnPages(
            urls => capturedUrl = urls,
            [
                new IndiaHsnCodeResponse
                {
                    AbsEntry = 19,
                    Chapter = "72",
                    Heading = "16",
                    SubHeading = "32",
                    ChapterID = "72.16.32",
                    Description = "ANGLES, BEAMS, CHANNELS, FLAT",
                },
            ]);

        var result = await _sut.SearchHsnCodesAsync(new PaginationRequest { PageNumber = 1, PageSize = 20 });

        capturedUrl.Should().Contain("/IndiaHsn?$select=");
        capturedUrl.Should().NotContain("IndiaHsns");
        capturedUrl.Should().NotContain("_GetList");
        result.Data.Should().HaveCount(1);
        result.Data![0].DisplayLabel.Should().Be("72.16.32 - ANGLES, BEAMS, CHANNELS, FLAT");
        result.Data[0].Description.Should().Be("ANGLES, BEAMS, CHANNELS, FLAT");
    }

    [Test]
    public async Task SearchSacCodesAsync_MatchesOnPartOfTheServiceCode()
    {
        SetupSacPages(
            _ => { },
            [
                new IndiaSacCodeResponse { AbsEntry = 11, ServiceCode = "998335" },
                new IndiaSacCodeResponse { AbsEntry = 12, ServiceCode = "997156" },
            ]);

        var result = await Search(_sut.SearchSacCodesAsync, "8335");

        result.Data.Should().HaveCount(1);
        result.Data![0].AbsEntry.Should().Be(11);
    }

    [Test]
    public async Task SearchSacCodesAsync_MatchesOnPartOfTheServiceName()
    {
        SetupSacPages(
            _ => { },
            [
                new IndiaSacCodeResponse { AbsEntry = 11, ServiceCode = "998335", ServiceName = "ENGINEERING SERVICES" },
                new IndiaSacCodeResponse { AbsEntry = 12, ServiceCode = "997156", ServiceName = "LEASING SERVICES" },
            ]);

        var result = await Search(_sut.SearchSacCodesAsync, "engineer");

        result.Data.Should().HaveCount(1);
        result.Data![0].AbsEntry.Should().Be(11);
    }

    [Test]
    public async Task SearchHsnCodesAsync_MatchesOnPartOfTheDescription()
    {
        SetupHsnPages(
            _ => { },
            [
                new IndiaHsnCodeResponse { AbsEntry = 19, ChapterID = "72.16.32", Description = "ANGLES, BEAMS, CHANNELS, FLAT" },
                new IndiaHsnCodeResponse { AbsEntry = 20, ChapterID = "84.81.80", Description = "TAPS, COCKS, VALVES" },
            ]);

        var result = await Search(_sut.SearchHsnCodesAsync, "valve");

        result.Data.Should().HaveCount(1);
        result.Data![0].AbsEntry.Should().Be(20);
    }

    /// <summary>
    /// Service Layer pages collections (20 rows by default), so a master with more rows than one page
    /// must be walked with $skip or the picker silently shows only the first page.
    /// </summary>
    [Test]
    public async Task SearchHsnCodesAsync_WalksEverySapPage()
    {
        // 200 = the page size the service asks SAP for; a full page means "there may be more".
        var firstPage = Enumerable.Range(1, 200)
            .Select(i => new IndiaHsnCodeResponse { AbsEntry = i, ChapterID = $"72.16.{i}" })
            .ToList();
        var secondPage = Enumerable.Range(201, 5)
            .Select(i => new IndiaHsnCodeResponse { AbsEntry = i, ChapterID = $"84.81.{i}" })
            .ToList();

        var urls = new List<string>();
        _http
            .SetupSequence(h => h.GetPageOrThrowAsync<IndiaHsnListEnvelope>(
                Capture.In(urls), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndiaHsnListEnvelope { Value = firstPage })
            .ReturnsAsync(new IndiaHsnListEnvelope { Value = secondPage });

        var result = await _sut.SearchHsnCodesAsync(new PaginationRequest { PageNumber = 1, PageSize = 20, IncludeTotalCount = true });

        urls.Should().HaveCount(2);
        urls[0].Should().NotContain("$skip=");
        urls[1].Should().Contain("$skip=200");
        result.TotalCount.Should().Be(205);
    }

    [Test]
    public async Task SearchSacCodesAsync_SapFailure_ReturnsEmptyListInsteadOfThrowing()
    {
        _http
            .Setup(h => h.GetPageOrThrowAsync<IndiaSacListEnvelope>(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unrecognized resource path"));

        var result = await _sut.SearchSacCodesAsync(new PaginationRequest { PageNumber = 1, PageSize = 20 });

        result.Data.Should().BeEmpty();
    }

    [Test]
    public async Task SearchHsnCodesAsync_SapFailure_ReturnsEmptyListInsteadOfThrowing()
    {
        _http
            .Setup(h => h.GetPageOrThrowAsync<IndiaHsnListEnvelope>(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service Not Found"));

        var result = await _sut.SearchHsnCodesAsync(new PaginationRequest { PageNumber = 1, PageSize = 20 });

        result.Data.Should().BeEmpty();
    }

    [Test]
    public async Task GetPurchaseUomOptionsAsync_ItemOnRealUomGroup_ReturnsGroupUnitsWithFactors()
    {
        SetupItem(new ItemsResponse
        {
            ItemCode = "ITM-GROUP",
            PurchaseUnit = "BOX",
            InventoryUom = "EA",
            UoMGroupEntry = 5,
            InventoryUoMEntry = 1,
        });
        SetupUomMaster(
            new SapUnitOfMeasurementResponse { AbsEntry = 1, Code = "EA", Name = "EACH" },
            new SapUnitOfMeasurementResponse { AbsEntry = 2, Code = "BOX", Name = "BOXES" },
            new SapUnitOfMeasurementResponse { AbsEntry = 3, Code = "PAL", Name = "PALLETS" });
        SetupUomGroup(new SapUnitOfMeasurementGroupResponse
        {
            AbsEntry = 5,
            Code = "BOX-EA",
            BaseUoM = 1,
            UoMGroupDefinitionCollection =
            [
                new SapUoMGroupDefinitionResponse { AlternateUoM = 1, AlternateQuantity = 1, BaseQuantity = 1, Active = "tYES" },
                new SapUoMGroupDefinitionResponse { AlternateUoM = 2, AlternateQuantity = 1, BaseQuantity = 12, Active = "tYES" },
                new SapUoMGroupDefinitionResponse { AlternateUoM = 3, AlternateQuantity = 1, BaseQuantity = 100, Active = "tNO" },
            ],
        });

        var options = await _sut.GetPurchaseUomOptionsAsync("ITM-GROUP");

        options.Should().HaveCount(2);
        options.Select(o => o.Code).Should().NotContain("PAL");
        var box = options.Single(o => o.Code == "BOX");
        box.Name.Should().Be("BOXES");
        box.UoMEntry.Should().Be(2);
        box.ItemsPerUnit.Should().Be(12);
        box.IsDefault.Should().BeTrue();
        box.Source.Should().Be("group");
        var each = options.Single(o => o.Code == "EA");
        each.UoMEntry.Should().Be(1);
        each.ItemsPerUnit.Should().Be(1);
        each.IsDefault.Should().BeFalse();
    }

    [Test]
    public async Task GetPurchaseUomOptionsAsync_ItemOnManualGroup_ReturnsPurchaseAndInventoryUnits()
    {
        SetupManualGroupItem();

        var options = await _sut.GetPurchaseUomOptionsAsync("RM5703813500380");

        options.Select(o => o.Code).Should().Equal("KGS", "MTR");
        options.Should().OnlyContain(o => o.Source == "master" && o.UoMEntry == null);
        options[0].IsDefault.Should().BeTrue();
        options[0].ItemsPerUnit.Should().Be(0.027);
        options[0].Name.Should().Be("KILOGRAMS");
        options[1].ItemsPerUnit.Should().Be(1);
        options[1].IsDefault.Should().BeFalse();
    }

    [Test]
    public async Task GetPurchaseUomOptionsAsync_SearchFiltersOnCodeOrName()
    {
        SetupManualGroupItem();

        var byCode = await _sut.GetPurchaseUomOptionsAsync("RM5703813500380", "mtr");
        var byName = await _sut.GetPurchaseUomOptionsAsync("RM5703813500380", "meters");

        byCode.Select(o => o.Code).Should().Equal("MTR");
        byName.Select(o => o.Code).Should().Equal("MTR");
    }

    [Test]
    public async Task GetPurchaseUomOptionsAsync_UnknownItem_ReturnsEmptyList()
    {
        _http
            .Setup(h => h.GetAsync<SapItemsResponse>(It.IsAny<string>(), true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapItemsResponse { Value = [] });
        SetupUomMaster(new SapUnitOfMeasurementResponse { AbsEntry = 1, Code = "MTR", Name = "METERS" });

        var options = await _sut.GetPurchaseUomOptionsAsync("NOPE");

        options.Should().BeEmpty();
    }

    private void SetupManualGroupItem()
    {
        SetupItem(new ItemsResponse
        {
            ItemCode = "RM5703813500380",
            PurchaseUnit = "KGS",
            PurchaseItemsPerUnit = 0.027,
            InventoryUom = "MTR",
            UoMGroupEntry = -1,
            InventoryUoMEntry = -1,
        });
        SetupUomMaster(
            new SapUnitOfMeasurementResponse { AbsEntry = -1, Code = "Manual", Name = "Manual" },
            new SapUnitOfMeasurementResponse { AbsEntry = 1, Code = "MTR", Name = "METERS" },
            new SapUnitOfMeasurementResponse { AbsEntry = 3, Code = "LTR", Name = "LITRES" },
            new SapUnitOfMeasurementResponse { AbsEntry = 4, Code = "KGS", Name = "KILOGRAMS" });
    }

    private void SetupItem(ItemsResponse item) =>
        _http
            .Setup(h => h.GetAsync<SapItemsResponse>(It.IsAny<string>(), true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapItemsResponse { Value = [item] });

    private void SetupUomMaster(params SapUnitOfMeasurementResponse[] units) =>
        _http
            .Setup(h => h.GetPageOrThrowAsync<SapUnitOfMeasurementsResponse>(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapUnitOfMeasurementsResponse { Value = units.ToList() });

    private void SetupUomGroup(SapUnitOfMeasurementGroupResponse group) =>
        _http
            .Setup(h => h.GetOrThrowAsync<SapUnitOfMeasurementGroupsResponse>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapUnitOfMeasurementGroupsResponse { Value = [group] });

    private void SetupHsnPages(Action<string> captureUrl, List<IndiaHsnCodeResponse> rows) =>
        _http
            .Setup(h => h.GetPageOrThrowAsync<IndiaHsnListEnvelope>(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, CancellationToken>((url, _, _) => captureUrl(url))
            .ReturnsAsync(new IndiaHsnListEnvelope { Value = rows });

    private void SetupSacPages(Action<string> captureUrl, List<IndiaSacCodeResponse> rows) =>
        _http
            .Setup(h => h.GetPageOrThrowAsync<IndiaSacListEnvelope>(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, CancellationToken>((url, _, _) => captureUrl(url))
            .ReturnsAsync(new IndiaSacListEnvelope { Value = rows });

    private static Task<PaginationResponse<List<T>>> Search<T>(
        Func<PaginationRequest, CancellationToken, Task<PaginationResponse<List<T>>>> search,
        string term) =>
        search(
            new PaginationRequest
            {
                PageNumber = 1,
                PageSize = 20,
                Filters = [new FilterModel { Field = "__search", Value = term }],
            },
            CancellationToken.None);
}
