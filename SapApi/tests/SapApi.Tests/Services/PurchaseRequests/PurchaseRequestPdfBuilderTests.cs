using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Caching;
using SapApi.Infrastructure.Services.PurchaseRequests;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Enums;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services.PurchaseRequests;

[TestFixture]
public class PurchaseRequestPdfBuilderTests
{
    private Mock<IHttpRequestHandler> _http = null!;
    private PurchaseRequestPdfBuilder _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _http = new Mock<IHttpRequestHandler>();
        var sapLogin = new Mock<ISapLoginService>();
        sapLogin.Setup(s => s.SapLoginAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var companyDb = new Mock<ICurrentCompanyDbAccessor>();
        companyDb.Setup(c => c.GetCompanyDbName()).Returns(SapCompanyDatabase.PBBPL_UAT.ToString());

        var cache = new SapMasterDataCache(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        var masterData = new SapMasterDataService(_http.Object, sapLogin.Object, cache, companyDb.Object);
        _sut = new PurchaseRequestPdfBuilder(masterData);

        _http
            .Setup(h => h.GetAsync<SapGetAllBranchesResponse>(
                It.Is<string>(url => url.Contains("BusinessPlaces", StringComparison.OrdinalIgnoreCase)),
                true,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapGetAllBranchesResponse
            {
                Value = [new SapBranchesResponse { BplId = 1, BplName = "Privilege Biksons Boilers Pvt Ltd", Address = "Supa MIDC, Ahilyanagar" }],
            });
    }

    private static SapPurchaseRequestsResponse MinimalPr() => new()
    {
        DocEntry = 5,
        DocNum = 262710000,
        BPLId = 1,
        Project = "PB/R&M/25262053",
        DocDate = new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc),
        UDispachAdd = "KRANTI S S K LTD, SANGLI, MH, 416309",
        Comments = "Urgent — for line stoppage",
        RequesterName = "Laxman Gawali",
        RequesterEmail = "laxman@example.com",
        DocumentLines =
        [
            new SapInventoryTransferItemsRequests
            {
                ItemCode = "CO6920400011100266",
                ItemDescription = "FILLER WIRE 2.4 MM THK 80S B2 ADOR MAKE",
                FreeText = "As per drawing",
                Quantity = 60,
                UoMCode = "KG",
            },
        ],
    };

    [Test]
    public async Task Header_comes_from_the_branch_and_first_line()
    {
        var result = await _sut.BuildPlaceholdersAsync(MinimalPr());

        result["entityName"].Should().Be("Privilege Biksons Boilers Pvt Ltd");
        result["entityAddress"].Should().Be("Supa MIDC, Ahilyanagar");
        result["prNo"].Should().Be("262710000");
        result["postingDate"].Should().Be("24/08/2026");
        result["productNo"].Should().Be("CO6920400011100266");
        result["productDescription"].Should().Be("FILLER WIRE 2.4 MM THK 80S B2 ADOR MAKE");
        result["projectCode"].Should().Be("PB/R&amp;M/25262053");
        result["dispatchAddress"].Should().Be("KRANTI S S K LTD, SANGLI, MH, 416309");
        result["remarks"].Should().Be("Urgent — for line stoppage");
    }

    [Test]
    public async Task Rows_include_item_code_description_free_text_qty_and_unit()
    {
        var result = await _sut.BuildPlaceholdersAsync(MinimalPr());

        result["rows"].Should().Contain("CO6920400011100266");
        result["rows"].Should().Contain("FILLER WIRE 2.4 MM THK 80S B2 ADOR MAKE");
        result["rows"].Should().Contain("As per drawing");
        result["rows"].Should().Contain("60");
        result["rows"].Should().Contain("KG");
    }

    [Test]
    public async Task Prepared_by_comes_from_the_requester_fields_approved_by_is_blank()
    {
        var result = await _sut.BuildPlaceholdersAsync(MinimalPr());

        result["preparedByName"].Should().Be("Laxman Gawali");
        result["preparedByEmail"].Should().Be("laxman@example.com");
        result["approvedByName"].Should().BeEmpty();
        result["approvedByEmail"].Should().BeEmpty();
    }

    [Test]
    public async Task No_lines_prints_a_placeholder_row_instead_of_an_empty_table()
    {
        var pr = MinimalPr();
        pr.DocumentLines = [];

        var result = await _sut.BuildPlaceholdersAsync(pr);

        result["rows"].Should().Contain("No line items");
        result["productNo"].Should().BeEmpty();
    }
}
