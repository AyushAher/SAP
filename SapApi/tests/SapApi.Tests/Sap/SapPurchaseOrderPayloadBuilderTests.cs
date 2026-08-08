using FluentAssertions;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Tests.Sap;

[TestFixture]
public class SapPurchaseOrderPayloadBuilderTests
{
    [Test]
    public void Prepare_Create_StripsCalculatedFields_AndMapsDates()
    {
        var source = new SapPurchaseOrdersResponse
        {
            CardCode = "V001",
            NumAtCard = "REF-1",
            DocTotal = 9999,
            VatSum = 100,
            DocumentStatus = "bost_Open",
            DocNum = 55,
            PostingDate = new DateTime(2026, 7, 1),
            DocDueDate = new DateTime(2026, 7, 15),
            BPLId = 2,
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests
                {
                    ItemCode = "I1",
                    Quantity = 2,
                    UnitPrice = 10,
                    DiscountPercent = 5,
                    WarehouseCode = "01",
                    TaxCode = "IGST18",
                    HSNEntry = 42,
                    SACEntry = null,
                    UoMCode = "NOS",
                    ProjectCode = "P1",
                    CostingCode = "CC1",
                    LineTotal = 19,
                    TaxTotal = 3,
                    GrossTotal = 22,
                },
            ],
        };

        var payload = SapPurchaseOrderPayloadBuilder.Prepare(source, isUpdate: false);

        payload.DocTotal.Should().BeNull();
        payload.VatSum.Should().BeNull();
        payload.DocNum.Should().BeNull();
        payload.DocumentStatus.Should().BeNull();
        payload.DocDate.Should().Be(new DateTime(2026, 7, 1));
        payload.TaxDate.Should().Be(new DateTime(2026, 7, 1));
        payload.DocDueDate.Should().Be(new DateTime(2026, 7, 15));
        payload.NumAtCard.Should().Be("REF-1");
        payload.BPLId.Should().Be(2);
        payload.Series.Should().BeNull();
        payload.DocumentLines.Should().HaveCount(1);
        var line = payload.DocumentLines![0];
        line.HSNEntry.Should().Be(42);
        line.UoMCode.Should().Be("NOS");
        line.ProjectCode.Should().Be("P1");
        line.CostingCode.Should().Be("CC1");
        line.DiscountPercent.Should().Be(5);
        line.LineTotal.Should().BeNull();
        line.TaxTotal.Should().BeNull();
        line.GrossTotal.Should().BeNull();
        line.LineNum.Should().BeNull();
    }

    [Test]
    public void Prepare_Create_IncludesOtherTermsUdfJsonNames()
    {
        var source = new SapPurchaseOrdersResponse
        {
            CardCode = "V001",
            UDelTerms = "FOB",
            UInspectionBy = "QC",
            UTransportation = "Road",
            USupervision = "Site",
            UTransitIns = "Vendor",
            UDrawDocs = "GA",
            ULoading = "Vendor",
            UWarranty = "12m",
            UUnloading = "Buyer",
            UOtherRemark = "Careful",
            UPainting = "Epoxy",
            UTestCerts = "MTC",
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests { ItemCode = "I1", Quantity = 1, UnitPrice = 1 },
            ],
        };

        var payload = SapPurchaseOrderPayloadBuilder.Prepare(source, isUpdate: false);
        var json = System.Text.Json.JsonSerializer.Serialize(payload);

        json.Should().Contain("\"U_DL\":\"FOB\"");
        json.Should().Contain("\"U_INSPBY\":\"QC\"");
        json.Should().Contain("\"U_TRANS\":\"Road\"");
        json.Should().Contain("\"U_SUPR\":\"Site\"");
        json.Should().Contain("\"U_TRANINSU\":\"Vendor\"");
        json.Should().Contain("\"U_DRA_DOC\":\"GA\"");
        json.Should().Contain("\"U_LOAD\":\"Vendor\"");
        json.Should().Contain("\"U_WARR\":\"12m\"");
        json.Should().Contain("\"U_UN_LOAD\":\"Buyer\"");
        json.Should().Contain("\"U_ANOTHREM\":\"Careful\"");
        json.Should().Contain("\"U_PAIN\":\"Epoxy\"");
        json.Should().Contain("\"U_TC\":\"MTC\"");
        json.Should().NotContain("U_DelTerms");
        json.Should().NotContain("U_InspectionBy");
        json.Should().NotContain("U_Warranty");
    }

    [Test]
    public void Prepare_Update_KeepsDocEntryAndLineNum()
    {
        var source = new SapPurchaseOrdersResponse
        {
            DocEntry = 100,
            CardCode = "V001",
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests
                {
                    LineNum = 0,
                    ItemCode = "I1",
                    Quantity = 1,
                    UnitPrice = 5,
                },
            ],
        };

        var payload = SapPurchaseOrderPayloadBuilder.Prepare(source, isUpdate: true);

        payload.DocEntry.Should().Be(100);
        payload.DocumentLines![0].LineNum.Should().Be(0);
    }

    [Test]
    public void Prepare_ServiceDocument_MapsAccountCode_WithoutItemCode()
    {
        var source = new SapPurchaseOrdersResponse
        {
            CardCode = "V001",
            DocType = "dDocument_Service",
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests
                {
                    ItemDescription = "Transport",
                    AccountCode = "600000",
                    Quantity = 1,
                    UnitPrice = 500,
                    TaxCode = "IGST18",
                    SACEntry = 11,
                    ItemCode = "SHOULD_IGNORE",
                    WarehouseCode = "01",
                },
            ],
        };

        var payload = SapPurchaseOrderPayloadBuilder.Prepare(source, isUpdate: false);

        payload.DocType.Should().Be("dDocument_Service");
        payload.DocumentLines.Should().HaveCount(1);
        var line = payload.DocumentLines![0];
        line.AccountCode.Should().Be("600000");
        line.ItemDescription.Should().Be("Transport");
        line.SACEntry.Should().Be(11);
        line.ItemCode.Should().BeNull();
        line.WarehouseCode.Should().BeNull();
        line.HSNEntry.Should().BeNull();
    }

    [Test]
    public void Prepare_Create_DropsShipToCode_WhenItMatchesDispatchCardCode()
    {
        var source = new SapPurchaseOrdersResponse
        {
            CardCode = "S000744",
            ShipToCode = "C000030",
            UCardCode = "C000030",
            UWarehouse = "Store1",
        };

        var payload = SapPurchaseOrderPayloadBuilder.Prepare(source, isUpdate: false);

        payload.ShipToCode.Should().BeNull();
        payload.UWarehouse.Should().BeNull();
        payload.UCardCode.Should().Be("C000030");
    }

    [Test]
    public void Prepare_Create_KeepsShipToCode_WhenItIsAddressName()
    {
        var source = new SapPurchaseOrdersResponse
        {
            CardCode = "S000744",
            ShipToCode = "PEARLS METALS",
            UCardCode = "C000030",
        };

        var payload = SapPurchaseOrderPayloadBuilder.Prepare(source, isUpdate: false);

        payload.ShipToCode.Should().Be("PEARLS METALS");
    }

    [Test]
    public void Prepare_MovesLegacyGstPercentFromUG3ToUG11()
    {
        var source = new SapPurchaseOrdersResponse
        {
            CardCode = "V001",
            UType1 = "Advance",
            UBasic1 = 20,
            UType2 = "Invoice",
            UBasic2 = 80,
            UType3 = "Invoice",
            UGst3 = 100,
        };

        var payload = SapPurchaseOrderPayloadBuilder.Prepare(source, isUpdate: false);

        payload.UGst3.Should().Be(0);
        payload.UGst11.Should().Be(100);
        payload.UType11.Should().Be("Invoice");
        payload.UType3.Should().BeNull();
        payload.UBasic1.Should().Be(20);
        payload.UBasic2.Should().Be(80);
        payload.UBasic11.Should().BeNull();
    }
}
