using FluentAssertions;
using Moq;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services.Sap;

/// <summary>
/// SAP Service Layer cannot filter on DocumentLines, so AP invoices and goods receipts are matched
/// to a purchase order in our code. Before these tests the lookup read a single Service Layer page
/// (20 rows) of the vendor's invoices and filtered that page, so every invoice of a vendor with more
/// open invoices than one page silently disappeared from the batch payment picker.
/// </summary>
[TestFixture]
public class SapVendorPaymentServiceTests
{
    private const int PurchaseOrderBaseType = 22;
    private const int GrpoBaseType = 20;
    private const int PageSize = 100;
    private const string CardCode = "S001053";
    private const int PoDocEntry = 5109;

    private Mock<IHttpRequestHandler> _http = null!;
    private SapVendorPaymentService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _http = new Mock<IHttpRequestHandler>();
        // Approval policy is only used by the payment write path, which these read tests never touch.
        _sut = new SapVendorPaymentService(_http.Object, null!);
    }

    [Test]
    public async Task GetApInvoicesForPurchaseOrder_InvoiceBeyondFirstPage_IsStillReturned()
    {
        var wanted = Invoice(9001, 262710501, PurchaseOrderBaseType, PoDocEntry);
        SetupPages(
            "PurchaseInvoices",
            [Filler(PageSize), [wanted]]);

        var response = await _sut.GetApInvoicesForPurchaseOrder(CardCode, PoDocEntry);

        response!.Value.Should().ContainSingle().Which.DocNum.Should().Be(262710501);
    }

    [Test]
    public async Task GetApInvoicesForPurchaseOrder_AsksSapForFullPagesAndSkipsAlreadyReadRows()
    {
        var urls = new List<string>();
        var pageSizes = new List<int>();
        SetupPages(
            "PurchaseInvoices",
            [Filler(PageSize), [Invoice(9001, 1, PurchaseOrderBaseType, PoDocEntry)]],
            urls,
            pageSizes);

        await _sut.GetApInvoicesForPurchaseOrder(CardCode, PoDocEntry);

        pageSizes.Should().AllBeEquivalentTo(PageSize);
        urls.Should().HaveCount(2);
        urls[0].Should().NotContain("$skip");
        urls[1].Should().Contain("$skip=100");
        urls.Should().AllSatisfy(url => url.Should().Contain("CardCode%20eq%20%27S001053%27"));
    }

    [Test]
    public async Task GetApInvoicesForPurchaseOrder_PoInvoicedDirectlyAndThroughGoodsReceipt_ReturnsBoth()
    {
        var direct = Invoice(9001, 1001, PurchaseOrderBaseType, PoDocEntry);
        var viaGrpo = Invoice(9002, 1002, GrpoBaseType, 7001);
        SetupPages("PurchaseInvoices", [[direct, viaGrpo]]);

        var response = await _sut.GetApInvoicesForPurchaseOrder(CardCode, PoDocEntry, [7001]);

        response!.Value!.Select(x => x.DocNum).Should().BeEquivalentTo([1001, 1002]);
    }

    [Test]
    public async Task GetApInvoicesForPurchaseOrder_InvoiceOfAnotherPurchaseOrder_IsExcluded()
    {
        SetupPages(
            "PurchaseInvoices",
            [[Invoice(9001, 1001, PurchaseOrderBaseType, 4242), Invoice(9002, 1002, GrpoBaseType, 8888)]]);

        var response = await _sut.GetApInvoicesForPurchaseOrder(CardCode, PoDocEntry, [7001]);

        response!.Value.Should().BeEmpty();
    }

    [Test]
    public async Task GetApInvoicesForPurchaseOrder_VendorWithEndlessInvoices_StopsAtPageCap()
    {
        var calls = 0;
        _http
            .Setup(h => h.GetPageAsync<GetAllSapPurchaseInvoicesResponse>(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                calls++;
                return new GetAllSapPurchaseInvoicesResponse
                {
                    Value = Filler(PageSize),
                    ODataNextLink = "PurchaseInvoices?$skip=more",
                };
            });

        var response = await _sut.GetApInvoicesForPurchaseOrder(CardCode, PoDocEntry);

        response!.Value.Should().BeEmpty();
        calls.Should().Be(20);
    }

    [Test]
    public async Task GetApInvoicesForPurchaseOrder_SapReturnsError_PropagatesErrorInsteadOfEmptyList()
    {
        _http
            .Setup(h => h.GetPageAsync<GetAllSapPurchaseInvoicesResponse>(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAllSapPurchaseInvoicesResponse
            {
                Error = new SapError { Code = -1, Message = new SapMessage { Value = "Service Layer unavailable" } },
            });

        var response = await _sut.GetApInvoicesForPurchaseOrder(CardCode, PoDocEntry);

        response!.Error.Should().NotBeNull();
    }

    [Test]
    public async Task GetApInvoicesForPurchaseOrder_VendorCodeWithQuote_IsEscapedInFilter()
    {
        var urls = new List<string>();
        SetupPages("PurchaseInvoices", [[]], urls);

        await _sut.GetApInvoicesForPurchaseOrder("O'BRIEN", PoDocEntry);

        urls.Single().Should().Contain("O%27%27BRIEN");
    }

    [Test]
    public async Task GetGrposForPurchaseOrder_PartlyInvoicedReceiptIsStillOpen_IsStillReturned()
    {
        var urls = new List<string>();
        var openReceipt = Invoice(7001, 3001, PurchaseOrderBaseType, PoDocEntry, "bost_Open");
        SetupPages("PurchaseDeliveryNotes", [[openReceipt]], urls);

        var response = await _sut.GetGrposForPurchaseOrder(CardCode, PoDocEntry);

        response!.Value.Should().ContainSingle().Which.DocEntry.Should().Be(7001);
        urls.Single().Should().NotContain("DocumentStatus");
    }

    [Test]
    public async Task GetGrposForPurchaseOrder_ReceiptBeyondFirstPage_IsStillReturned()
    {
        SetupPages(
            "PurchaseDeliveryNotes",
            [Filler(PageSize), [Invoice(7002, 3002, PurchaseOrderBaseType, PoDocEntry)]]);

        var response = await _sut.GetGrposForPurchaseOrder(CardCode, PoDocEntry);

        response!.Value.Should().ContainSingle().Which.DocEntry.Should().Be(7002);
    }

    private void SetupPages(
        string collection,
        IReadOnlyList<List<SapPurchaseInvoicesResponse>> pages,
        List<string>? capturedUrls = null,
        List<int>? capturedPageSizes = null)
    {
        _http
            .Setup(h => h.GetPageAsync<GetAllSapPurchaseInvoicesResponse>(
                It.Is<string>(url => url.Contains(collection)), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, int maxPageSize, CancellationToken _) =>
            {
                capturedUrls?.Add(url);
                capturedPageSizes?.Add(maxPageSize);

                var index = ReadSkip(url) / PageSize;
                var rows = index < pages.Count ? pages[index] : [];
                return new GetAllSapPurchaseInvoicesResponse
                {
                    Value = rows,
                    ODataNextLink = index + 1 < pages.Count ? $"{collection}?$skip={(index + 1) * PageSize}" : null,
                };
            });
    }

    private static int ReadSkip(string url)
    {
        const string marker = "$skip=";
        var start = url.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return 0;

        var value = url[(start + marker.Length)..].Split('&')[0];
        return int.TryParse(value, out var skip) ? skip : 0;
    }

    private static List<SapPurchaseInvoicesResponse> Filler(int count) =>
        Enumerable.Range(1, count).Select(i => Invoice(i, i, PurchaseOrderBaseType, 999_000 + i)).ToList();

    private static SapPurchaseInvoicesResponse Invoice(
        int docEntry,
        int docNum,
        int baseType,
        int baseEntry,
        string documentStatus = "bost_Open") => new()
        {
            DocEntry = docEntry,
            DocNum = docNum,
            CardCode = CardCode,
            DocumentStatus = documentStatus,
            DocumentLines = [new SapPurchaseInvoiceDocumentLines { LineNum = 0, BaseType = baseType, BaseEntry = baseEntry }],
        };
}
