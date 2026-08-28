using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Caching;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Infrastructure.Services.Sap;
using SapApi.Shared.Enums;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services.PurchaseOrders;

[TestFixture]
public class PurchaseOrderPdfBuilderTests
{
    private Mock<IHttpRequestHandler> _http = null!;
    private PurchaseOrderPdfBuilder _sut = null!;

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
        _sut = new PurchaseOrderPdfBuilder(masterData);
    }

    [Test]
    public async Task Printed_by_uses_the_supplied_full_name()
    {
        SetupBranchAndProject();

        var result = await _sut.BuildPlaceholdersAsync(MinimalOrder(), "Aditya Aher");

        result["userName"].Should().Be("Aditya Aher");
        result["printedOn"].Should().EndWith(" IST");
    }

    [Test]
    public async Task Pan_and_gst_come_from_the_po_branch()
    {
        SetupBranchAndProject(
            gst: "27AABCP1234A1Z5",
            pan: "AABCP1234A");

        var result = await _sut.BuildPlaceholdersAsync(MinimalOrder(), "Aditya Aher");

        result["bplGst"].Should().Be("27AABCP1234A1Z5");
        result["bplPan"].Should().Be("AABCP1234A");
        result["bplName"].Should().Be("Pune Branch");
    }

    [Test]
    public async Task Pan_falls_back_to_the_gstin_embedded_pan_when_branch_udf_is_empty()
    {
        SetupBranchAndProject(gst: "27AABCP1234A1Z5", pan: null);

        var result = await _sut.BuildPlaceholdersAsync(MinimalOrder(), "Aditya Aher");

        result["bplGst"].Should().Be("27AABCP1234A1Z5");
        result["bplPan"].Should().Be("AABCP1234A");
    }

    [Test]
    public async Task Project_display_includes_code_and_name()
    {
        SetupBranchAndProject(projectName: "FORBESVYNCKE (PO NO:XX3824)");

        var result = await _sut.BuildPlaceholdersAsync(MinimalOrder(), "Aditya Aher");

        result["projectNo"].Should().Be("PB/R&amp;M/25262053");
        result["projectName"].Should().Be("FORBESVYNCKE (PO NO:XX3824)");
        result["projectDisplay"].Should().Be("PB/R&amp;M/25262053 - FORBESVYNCKE (PO NO:XX3824)");
    }

    [Test]
    public async Task Terms_of_contract_print_the_clauses_with_price_basis_from_the_po()
    {
        SetupBranchAndProject();
        var order = MinimalOrder();
        order.UPriceBasis = "F.O.R.";
        order.UDelTerms = "4 weeks";
        order.UInspectionBy = "PBBPL QC";
        order.UTransportation = "Road";
        order.UBasic1 = 20;
        order.UType1 = "Advance";
        order.UDes1 = "20% Basic As Advance";
        order.UGst11 = 100;
        order.UType11 = "Invoice";
        order.UDes11 = "100% GST Against Invoice";

        var result = await _sut.BuildPlaceholdersAsync(order, "Aditya Aher");

        result["@terms"].Should().Contain("PAYMENT TERMS");
        result["@terms"].Should().Contain("1. 20% Basic As Advance");
        result["@terms"].Should().Contain("2. 100% GST Against Invoice");
        var paymentHead = result["@terms"].IndexOf("PAYMENT TERMS", StringComparison.Ordinal);
        var termsHead = result["@terms"].IndexOf("TERMS &amp; CONDITIONS", StringComparison.Ordinal);
        paymentHead.Should().BeGreaterThanOrEqualTo(0);
        termsHead.Should().BeGreaterThan(paymentHead);
        result["@terms"].Should().NotContain("STANDARD");
        result["@terms"].Should().NotContain("To Be Printed");
        result["@terms"].Should().Contain("Order is placed on F.O.R. basis.");
        result["@terms"].Should().NotContain("Order is placed on ____ basis.");
        result["@terms"].Should().Contain("delivered/completed within 4 weeks.");
        result["@terms"].Should().Contain("inspection and approval by PBBPL QC.");
        result["@terms"].Should().Contain("Packaging shall be Road.");
        result["@terms"].Should().NotContain("____");
        result["@terms"].Should().Contain("<br>");
        result["@terms"].Should().Contain("courts at Pune, Maharashtra");
        result["@terms"].Should().Contain("18 months from date of supply");
        result["@terms"].Should().Contain("purchase.pune@privilegeboilers.com");
    }

    [Test]
    public async Task Amount_in_figures_and_words_use_total_basic_excluding_gst()
    {
        SetupBranchAndProject();
        var order = MinimalOrder();
        order.DocCurrency = "INR";
        order.DocTotal = 1180;
        order.VatSum = 180;
        order.DocumentLines =
        [
            new()
            {
                ItemCode = "RM1",
                ItemDescription = "Beam",
                Quantity = 1,
                UnitPrice = 1000,
                LineTotal = 1000,
            },
        ];

        var result = await _sut.BuildPlaceholdersAsync(order, "Aditya Aher");

        result["amountFigures"].Should().Be("INR 1,000.00");
        result["amountWords"].Should().Be("One Thousand Rupees Only");
        result["amountFigures"].Should().NotContain("1,180");
        result["amountWords"].Should().NotContain("One Thousand One Hundred");
    }

    [Test]
    public async Task Ship_to_contact_comes_from_the_contact_person_field()
    {
        SetupBranchAndProject();
        SetupWarehouse("Store1", FactoryWarehouse());
        var order = MinimalOrder();
        order.UShipTo = "KIRAN DURAPE";
        order.UDispachAdd = "Should not appear as contact";
        order.DocumentLines = [new() { WarehouseCode = "Store1" }];

        var result = await _sut.BuildPlaceholdersAsync(order, "Aditya Aher");

        result["shipToContact"].Should().Be("KIRAN DURAPE");
    }

    [Test]
    public async Task Factory_or_office_ship_to_address_comes_from_the_warehouse()
    {
        SetupBranchAndProject();
        SetupWarehouse("Store1", FactoryWarehouse());
        var order = MinimalOrder();
        order.UShipTo = "Ravi Kumar";
        order.UDispachAdd = "KRANTI S S K LTD, SANGLI, MH, 416309";
        order.UDisId = "C000030";
        order.ShipToCode = "PLOT NO- X-38,";
        order.DocumentLines = [new() { WarehouseCode = "Store1" }];

        var result = await _sut.BuildPlaceholdersAsync(order, "Aditya Aher");

        result["shipToName"].Should().Be("Privilege Biksons Boilers Pvt. Ltd. (Supa Works)");
        result["shipToAddress"].Should().Be("Plot No. B-61, Tal-Parner, Supa MIDC, Dist. Ahilayanagar, Ahilayanagar");
        result["shipToPin"].Should().Be("414301");
        result["shipToState"].Should().Be("MH");
        result["shipToAddress"].Should().NotContain("KRANTI");
        result["shipToContact"].Should().Be("Ravi Kumar");
        result["shipToGst"].Should().Be("27AABCP1234A1Z5");
        result["shipToName"].Should().NotContain("PLOT");
    }

    [Test]
    public async Task Office_ship_to_address_comes_from_the_warehouse()
    {
        SetupBranchAndProject();
        SetupWarehouse("Store5", new WarehouseResponse
        {
            WarehouseCode = "Store5",
            WarehouseName = "Pune (PBBPL)",
            Street = "Viman Nagar",
            City = "Pune",
            State = "MH",
            ZipCode = "411014",
        });
        var order = MinimalOrder();
        order.UWarehouse = "Store5";
        order.UDispachAdd = "Customer site address";
        order.UShipTo = "Office Contact";

        var result = await _sut.BuildPlaceholdersAsync(order, "Aditya Aher");

        result["shipToAddress"].Should().Be("Viman Nagar, Pune");
        result["shipToPin"].Should().Be("411014");
        result["shipToContact"].Should().Be("Office Contact");
        result["shipToAddress"].Should().NotContain("Customer site");
    }

    [Test]
    public async Task Bp_loc_ship_to_address_comes_from_the_ship_to_field()
    {
        SetupBranchAndProject();
        SetupDispatchToBp("C000030", "KRANTI SSK", zip: "416309", state: "MH", gstin: "27AAAAA0000A1Z5");
        var order = MinimalOrder();
        order.UDisId = "C000030";
        order.UDispachAdd = "KRANTI S S K LTD,, A/P KUNDAL,, VITA ROAD,, TAL. PALUS,, SANGLI, MH, 416309, IN";
        order.UShipTo = "KIRAN DURAPE";
        order.DocumentLines = [new() { WarehouseCode = "PBPL(S)" }];

        var result = await _sut.BuildPlaceholdersAsync(order, "Aditya Aher");

        result["shipToName"].Should().Be("C000030 - KRANTI SSK");
        result["shipToAddress"].Should().Be("KRANTI S S K LTD,, A/P KUNDAL,, VITA ROAD,, TAL. PALUS,, SANGLI, MH, 416309, IN");
        result["shipToPin"].Should().Be("416309");
        result["shipToState"].Should().Be("MH");
        result["shipToGst"].Should().Be("27AAAAA0000A1Z5");
        result["shipToContact"].Should().Be("KIRAN DURAPE");
    }

    [Test]
    public async Task Item_rows_print_sap_document_special_lines_after_the_matching_line()
    {
        SetupBranchAndProject();
        var order = MinimalOrder();
        order.Comments = "header comments are not special lines";
        order.DocumentLines =
        [
            new()
            {
                LineNum = 0,
                ItemCode = "RM1",
                ItemDescription = "Plate",
                Quantity = 1,
                UnitPrice = 10,
                LineTotal = 10,
            },
            new()
            {
                LineNum = 1,
                ItemCode = "RM2",
                ItemDescription = "Rod",
                Quantity = 1,
                UnitPrice = 10,
                LineTotal = 10,
            },
        ];
        order.DocumentSpecialLines =
        [
            new SapDocumentSpecialLine
            {
                AfterLineNumber = 0,
                LineType = "dslt_Text",
                LineText = "Make as per drawing D-101",
            },
        ];

        var result = await _sut.BuildPlaceholdersAsync(order, "Aditya Aher");

        result["@items"].Should().Contain("Make as per drawing D-101");
        result["@items"].Should().Contain("Document Special Lines: Make as per drawing D-101");
        result["@items"].Should().NotContain("header comments");
        var firstItem = result["@items"].IndexOf("RM1", StringComparison.Ordinal);
        var special = result["@items"].IndexOf("Make as per drawing D-101", StringComparison.Ordinal);
        var secondItem = result["@items"].IndexOf("RM2", StringComparison.Ordinal);
        firstItem.Should().BeGreaterThanOrEqualTo(0);
        special.Should().BeGreaterThan(firstItem);
        secondItem.Should().BeGreaterThan(special);
    }

    [Test]
    public async Task Empty_document_special_lines_are_omitted_from_the_layout()
    {
        SetupBranchAndProject();
        var order = MinimalOrder();
        order.DocumentLines =
        [
            new() { LineNum = 0, ItemCode = "RM1", ItemDescription = "Plate", Quantity = 1, LineTotal = 10 },
        ];

        var result = await _sut.BuildPlaceholdersAsync(order, "Aditya Aher");

        result["@items"].Should().Contain("RM1");
        result["@items"].Should().NotContain("Document Special Lines");
    }

    [Test]
    public async Task Line_free_text_is_used_when_sap_special_lines_are_missing()
    {
        SetupBranchAndProject();
        var order = MinimalOrder();
        order.DocumentLines =
        [
            new()
            {
                LineNum = 0,
                ItemCode = "RM1",
                ItemDescription = "Plate",
                FreeText = "Cut to 3.2 m",
                Quantity = 1,
                LineTotal = 10,
            },
        ];

        var result = await _sut.BuildPlaceholdersAsync(order, "Aditya Aher");

        result["@items"].Should().Contain("Document Special Lines: Cut to 3.2 m");
    }

    private static WarehouseResponse FactoryWarehouse() => new()
    {
        WarehouseCode = "Store1",
        WarehouseName = "Privilege Biksons Boilers Pvt. Ltd. (Supa Works)",
        StreetNo = "Plot No. B-61, Tal-Parner",
        Block = "Supa MIDC, Dist. Ahilayanagar",
        City = "Ahilayanagar",
        State = "MH",
        ZipCode = "414301",
        Country = "IN",
    };

    private void SetupWarehouse(string code, WarehouseResponse warehouse)
    {
        _http
            .Setup(h => h.GetAsync<SapWarehousesResponse>(
                It.Is<string>(url =>
                    url.Contains("/Warehouses", StringComparison.OrdinalIgnoreCase)
                    && url.Contains(code, StringComparison.OrdinalIgnoreCase)),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapWarehousesResponse { Value = [warehouse] });
    }

    private void SetupDispatchToBp(string cardCode, string cardName, string zip, string state, string gstin)
    {
        _http
            .Setup(h => h.GetAsync<SapBusinessPartner>(
                It.Is<string>(url =>
                    url.Contains("/BusinessPartners", StringComparison.OrdinalIgnoreCase)
                    && url.Contains(cardCode, StringComparison.OrdinalIgnoreCase)),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapBusinessPartner
            {
                CardCode = cardCode,
                CardName = cardName,
                BPAddresses =
                [
                    new SapBusinessPartnerAddress
                    {
                        AddressType = "bo_ShipTo",
                        ZipCode = zip,
                        State = state,
                        Gstin = gstin,
                    },
                ],
            });
    }

    private static SapPurchaseOrdersResponse MinimalOrder() => new()
    {
        DocEntry = 10,
        DocNum = 252610001,
        BPLId = 2,
        Project = "PB/R&M/25262053",
        DocumentLines = [],
    };

    private void SetupBranchAndProject(
        string gst = "27AABCP1234A1Z5",
        string? pan = "AABCP1234A",
        string projectName = "FORBESVYNCKE (PO NO:XX3824)")
    {
        _http
            .Setup(h => h.GetAsync<SapGetAllBranchesResponse>(
                It.Is<string>(url => url.Contains("BusinessPlaces", StringComparison.OrdinalIgnoreCase)),
                true,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapGetAllBranchesResponse
            {
                Value =
                [
                    new SapBranchesResponse
                    {
                        BplId = 2,
                        BplName = "Pune Branch",
                        Address = "Pune",
                        FederalTaxID = gst,
                        PanNo = pan,
                    },
                ],
            });

        _http
            .Setup(h => h.GetAsync<SapGetAllProjectDetailsResponse>(
                It.Is<string>(url => url.Contains("/Projects", StringComparison.OrdinalIgnoreCase)),
                true,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SapGetAllProjectDetailsResponse
            {
                Value =
                [
                    new SapProjectDetailsResponse
                    {
                        ProjectCode = "PB/R&M/25262053",
                        ProjectName = projectName,
                    },
                ],
            });
    }
}
