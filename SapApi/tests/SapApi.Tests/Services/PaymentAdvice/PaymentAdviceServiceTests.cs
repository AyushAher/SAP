using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Caching;
using SapApi.Infrastructure.Services.PaymentAdvice;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Enums;
using SapApi.Shared.Responses;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services.PaymentAdvice;

[TestFixture]
public class PaymentAdviceServiceTests
{
    private Mock<IHttpRequestHandler> _http = null!;
    private PaymentAdviceService _sut = null!;

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
        _sut = new PaymentAdviceService(masterData, Mock.Of<IPdfService>());

        _http
            .Setup(h => h.GetAsync<SapGetAllBranchesResponse>(
                It.Is<string>(url => url.Contains("BusinessPlaces", StringComparison.OrdinalIgnoreCase)),
                true,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapGetAllBranchesResponse
            {
                Value = [new SapBranchesResponse { BplId = 1, BplName = "Privilege Biksons Boilers Pvt Ltd" }],
            });
    }

    private static StageWisePayment MinimalRecord() => new()
    {
        Id = 42,
        GrossAmount = 100000,
        GstAmount = 18000,
        Tds = 1000,
        UtrNo = "UTR123456",
        UtrDate = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc),
    };

    private static StageWisePaymentPageDataResponse MinimalPageData() => new()
    {
        PurchaseOrder = new SapPurchaseOrdersResponse
        {
            DocEntry = 501,
            DocNum = 25000501,
            BPLId = 1,
            CardCode = "V001",
            CardName = "Acme Vendor",
        },
        VendorBankAccounts =
        [
            new VendorBankAccountOption
            {
                BankCode = "HDFC (PBBPL)",
                AccountNo = "00011122233",
                Branch = "Pune Camp",
                Display = "HDFC (PBBPL) / 00011122233 / Pune Camp",
            },
            new VendorBankAccountOption
            {
                BankCode = "ICICI (PBBPL)",
                AccountNo = "99988877766",
                Branch = "Kothrud",
                Display = "ICICI (PBBPL) / 99988877766 / Kothrud",
            },
        ],
    };

    [Test]
    public async Task Amounts_are_gross_plus_gst_minus_tds()
    {
        var result = await _sut.BuildPlaceholdersAsync(MinimalRecord(), MinimalPageData(), null, null, null);

        result["GrossAmount"].Should().Be((118000d).ToString("N2"));
        result["TDSAmount"].Should().Be((1000d).ToString("N2"));
        result["NetAmount"].Should().Be((117000d).ToString("N2"));
    }

    [Test]
    public async Task Vendor_and_po_come_from_the_purchase_order()
    {
        var result = await _sut.BuildPlaceholdersAsync(MinimalRecord(), MinimalPageData(), null, null, null);

        result["VendorCode"].Should().Be("V001");
        result["VendorName"].Should().Be("Acme Vendor");
        result["PurchaseOrder"].Should().Be("25000501");
        result["CompanyName"].Should().Be("Privilege Biksons Boilers Pvt Ltd");
    }

    [Test]
    public async Task Bank_reference_and_date_come_from_the_record_when_no_batch_context_is_given()
    {
        var result = await _sut.BuildPlaceholdersAsync(MinimalRecord(), MinimalPageData(), null, null, null);

        result["BankReferenceNo"].Should().Be("UTR123456");
        result["PaymentDate"].Should().Be("10/09/2026");
    }

    [Test]
    public async Task Batch_payment_date_overrides_the_records_own_utr_date()
    {
        var batchPaymentDate = new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc);

        var result = await _sut.BuildPlaceholdersAsync(MinimalRecord(), MinimalPageData(), null, batchPaymentDate, null);

        result["PaymentDate"].Should().Be("12/09/2026");
    }

    [Test]
    public async Task Selects_the_vendor_bank_account_matching_the_batchs_vendor_bank_code()
    {
        var result = await _sut.BuildPlaceholdersAsync(MinimalRecord(), MinimalPageData(), "ICICI (PBBPL)", null, null);

        result["BankName"].Should().Be("ICICI (PBBPL)");
        result["AccountNumber"].Should().Be("99988877766");
        result["BranchName"].Should().Be("Kothrud");
    }

    [Test]
    public async Task Falls_back_to_the_first_bank_account_when_no_code_is_given()
    {
        var result = await _sut.BuildPlaceholdersAsync(MinimalRecord(), MinimalPageData(), null, null, null);

        result["BankName"].Should().Be("HDFC (PBBPL)");
        result["AccountNumber"].Should().Be("00011122233");
    }

    [Test]
    public async Task Remarks_defaults_to_empty_and_ifsc_is_always_blank()
    {
        var result = await _sut.BuildPlaceholdersAsync(MinimalRecord(), MinimalPageData(), null, null, null);

        result["Remarks"].Should().BeEmpty();
        result["IFSCCode"].Should().BeEmpty();
    }

    [Test]
    public async Task Remarks_pass_through_when_provided()
    {
        var result = await _sut.BuildPlaceholdersAsync(MinimalRecord(), MinimalPageData(), null, null, "Thank you for your patience.");

        result["Remarks"].Should().Be("Thank you for your patience.");
    }
}
