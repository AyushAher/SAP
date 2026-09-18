using System.Text.Json;
using FluentAssertions;
using SapApi.Shared;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using SapApi.Shared.Sap;

namespace SapApi.Tests.Sap;

[TestFixture]
public class SapPurchaseRequestPayloadBuilderTests
{
    [Test]
    public void Prepare_Create_StripsCalculatedFields_AndMapsDates()
    {
        var source = new SapPurchaseRequestsResponse
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
                    LocationCode = 4,
                    TaxCode = "IGST18",
                    HSNEntry = 42,
                    SACEntry = null,
                    UoMCode = "NOS",
                    ProjectCode = "P1",
                    CostingCode = "CC1",
                    FreeText = "Rush delivery",
                    LineTotal = 19,
                    TaxTotal = 3,
                    GrossTotal = 22,
                },
            ],
        };

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: false);

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
        // No UoMEntry: do not send UoMCode or MeasureUnit — SAP derives MeasureUnit from the factor.
        line.UoMCode.Should().BeNull();
        line.MeasureUnit.Should().BeNull();
        line.ProjectCode.Should().Be("P1");
        line.FreeText.Should().BeNull();
        line.UFreeTxt.Should().BeNull();
        line.LocationCode.Should().Be(4);
        payload.DocumentSpecialLines.Should().ContainSingle();
        payload.DocumentSpecialLines![0].LineType.Should().Be("dslt_Text");
        payload.DocumentSpecialLines[0].LineText.Should().Be("Rush delivery");
        payload.DocumentSpecialLines[0].AfterLineNumber.Should().Be(0);
        line.CostingCode.Should().BeNull();
        line.UProdNo.Should().BeNull();
        line.DiscountPercent.Should().Be(5);
        line.LineTotal.Should().BeNull();
        line.TaxTotal.Should().BeNull();
        line.GrossTotal.Should().BeNull();
        line.LineNum.Should().Be(0);
    }

    [Test]
    public void Prepare_Create_UsesHeaderDocumentSpecialLinesWhenLineHasNoFreeText()
    {
        var source = new SapPurchaseRequestsResponse
        {
            CardCode = "V001",
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests { ItemCode = "I1", Quantity = 1, UnitPrice = 1 },
            ],
            DocumentSpecialLines =
            [
                new SapDocumentSpecialLine
                {
                    AfterLineNumber = 0,
                    LineType = "dslt_Text",
                    LineText = "From header collection",
                },
            ],
        };

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: false);
        payload.DocumentSpecialLines.Should().ContainSingle();
        payload.DocumentSpecialLines![0].LineText.Should().Be("From header collection");
        payload.DocumentSpecialLines[0].AfterLineNumber.Should().Be(0);
    }

    [Test]
    public void Prepare_Create_PutsLongFreeTextOnlyOnDocumentSpecialLines()
    {
        var longText = new string('x', 250);
        var source = new SapPurchaseRequestsResponse
        {
            CardCode = "V001",
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests
                {
                    ItemCode = "I1",
                    Quantity = 1,
                    UnitPrice = 1,
                    FreeText = longText,
                },
            ],
        };

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: false);
        payload.DocumentLines![0].FreeText.Should().BeNull();
        payload.DocumentLines[0].UFreeTxt.Should().BeNull();
        payload.DocumentSpecialLines.Should().ContainSingle();
        payload.DocumentSpecialLines![0].LineText.Should().Be(longText);
    }

    [Test]
    public void Prepare_Create_OmitsPurchaseOrderOnlyUdfs()
    {
        var source = new SapPurchaseRequestsResponse
        {
            CardCode = "V001",
            UDelTerms = "FOB",
            UGstText = "client must not override",
            UTdsText = "client must not override",
            UPackingForwarding = "IN OUR SCOPE",
            UTcDispatchAddress = "H.O. ADDRESS",
            UDisId = "C000030",
            UDispachAdd = "Pune plant",
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests { ItemCode = "I1", Quantity = 1, UnitPrice = 1 },
            ],
        };

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: false);
        var json = JsonSerializer.Serialize(payload);

        payload.UGstText.Should().BeNull();
        payload.UTdsText.Should().BeNull();
        payload.UDelTerms.Should().BeNull();
        payload.UPackingForwarding.Should().BeNull();
        payload.UDisId.Should().BeNull();
        payload.UGst1.Should().BeNull();
        json.Should().NotContain("U_GST_");
        json.Should().NotContain("U_TDS_");
        json.Should().NotContain("U_DL");
        json.Should().NotContain("U_PAC_FOR");
        json.Should().NotContain("U_DisID");
        json.Should().NotContain("\"U_G1\"");
    }

    [Test]
    public void Prepare_Create_DropsDisplayNameRequesterAndNonNumericReqCode()
    {
        var payload = SapPurchaseRequestPayloadBuilder.Prepare(
            new SapPurchaseRequestsResponse
            {
                Requester = "Aditya Aher",
                ReqCode = "Aditya Aher",
                ReqType = Constants.SapPurchaseRequestReqType.User,
                DocumentLines =
                [
                    new SapInventoryTransferItemsRequests { ItemCode = "I1", Quantity = 1, UnitPrice = 1 },
                ],
            },
            isUpdate: false);

        payload.Requester.Should().BeNull();
        payload.ReqCode.Should().BeNull();
        payload.ReqType.Should().BeNull();
        payload.RequesterName.Should().Be("Aditya Aher");
        JsonSerializer.Serialize(payload).Should().NotContain("ReqCode");
    }

    [Test]
    public void Prepare_Create_KeepsSapUserCodeRequesterWithoutReqCode()
    {
        var payload = SapPurchaseRequestPayloadBuilder.Prepare(
            new SapPurchaseRequestsResponse
            {
                Requester = "manager",
                ReqCode = "manager",
                ReqType = Constants.SapPurchaseRequestReqType.User,
                DocumentLines =
                [
                    new SapInventoryTransferItemsRequests { ItemCode = "I1", Quantity = 1, UnitPrice = 1 },
                ],
            },
            isUpdate: false);

        payload.Requester.Should().Be("manager");
        payload.ReqCode.Should().BeNull();
        payload.ReqType.Should().Be(Constants.SapPurchaseRequestReqType.User);
    }

    [Test]
    public void MergeOtherTermUdfFromSap_CopiesUnloadingAndAdditionalUdf()
    {
        var local = new SapPurchaseRequestsResponse { UUnloading = "old" };
        var sap = new SapPurchaseRequestsResponse
        {
            UUnloading = "Buyer",
            UTransportation = "Vendor",
            UTransitIns = "Inclusive",
            UPackingForwarding = "IN OUR SCOPE",
            UTcDispatchAddress = "H.O. ADDRESS",
            AdditionalUdf = new Dictionary<string, JsonElement>
            {
                ["U_PACK_FOR"] = JsonSerializer.SerializeToElement("Extra"),
            },
        };

        SapPurchaseRequestPayloadBuilder.MergeOtherTermUdfFromSap(local, sap);

        local.UUnloading.Should().Be("Buyer");
        local.UTransportation.Should().Be("Vendor");
        local.UTransitIns.Should().Be("Inclusive");
        local.UPackingForwarding.Should().Be("IN OUR SCOPE");
        local.UTcDispatchAddress.Should().Be("H.O. ADDRESS");
        local.AdditionalUdf.Should().ContainKey("U_PACK_FOR");
        local.AdditionalUdf!["U_PACK_FOR"].GetString().Should().Be("Extra");
    }

    [Test]
    public void Prepare_Update_KeepsDocEntryAndLineNum()
    {
        var source = new SapPurchaseRequestsResponse
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

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: true);

        payload.DocEntry.Should().Be(100);
        payload.DocumentLines![0].LineNum.Should().Be(0);
    }

    [Test]
    public void Prepare_Update_DoesNotInventLineNumForNewRows()
    {
        var source = new SapPurchaseRequestsResponse
        {
            DocEntry = 100,
            CardCode = "V001",
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests
                {
                    LineNum = 1,
                    ItemCode = "KEEP",
                    Quantity = 1,
                    UnitPrice = 5,
                },
                new SapInventoryTransferItemsRequests
                {
                    ItemCode = "NEW",
                    Quantity = 2,
                    UnitPrice = 3,
                },
            ],
        };

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: true);

        payload.DocumentLines.Should().HaveCount(2);
        payload.DocumentLines![0].LineNum.Should().Be(1);
        payload.DocumentLines[1].LineNum.Should().BeNull();
        payload.DocumentLines[1].ItemCode.Should().Be("NEW");
    }

    [Test]
    public void Prepare_Update_DoesNotCopyLocationCodeAcrossItemLines()
    {
        var source = new SapPurchaseRequestsResponse
        {
            DocEntry = 100,
            CardCode = "V001",
            DocType = "dDocument_Items",
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests
                {
                    LineNum = 0,
                    ItemCode = "I1",
                    Quantity = 1,
                    UnitPrice = 5,
                    WarehouseCode = "01",
                },
                new SapInventoryTransferItemsRequests
                {
                    LineNum = 1,
                    ItemCode = "I2",
                    Quantity = 1,
                    UnitPrice = 5,
                    WarehouseCode = "02",
                    LocationCode = 4,
                },
            ],
        };

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: true);

        payload.DocumentLines![0].LocationCode.Should().BeNull();
        payload.DocumentLines[1].LocationCode.Should().Be(4);
    }

    [Test]
    public void Prepare_ServiceDocument_MapsAccountCode_WithoutItemCode()
    {
        var source = new SapPurchaseRequestsResponse
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
                    LocationCode = 2,
                    GrossTotal = 1441.96,
                    InventoryQuantity = 1,
                },
            ],
        };

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: false);

        payload.DocType.Should().Be("dDocument_Service");
        payload.DocumentLines.Should().HaveCount(1);
        var line = payload.DocumentLines![0];
        line.AccountCode.Should().Be("600000");
        line.ItemDescription.Should().Be("Transport");
        line.SACEntry.Should().Be(11);
        line.ItemCode.Should().BeNull();
        line.WarehouseCode.Should().BeNull();
        line.HSNEntry.Should().BeNull();
        line.GrossTotal.Should().BeNull();
        line.InventoryQuantity.Should().BeNull();
        line.LocationCode.Should().Be(2);
    }

    [Test]
    public void Prepare_Create_AssignsLineNumAndCopiesLocationCodeOnEveryServiceLine()
    {
        var source = new SapPurchaseRequestsResponse
        {
            CardCode = "S000035",
            DocType = "dDocument_Service",
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests
                {
                    LineNum = 0,
                    Quantity = 1,
                    UnitPrice = 12000,
                    TaxCode = "SGST18",
                    ItemDescription = "PLANT AND MACHINERY",
                    AccountCode = "_SYS00000000677",
                    SACEntry = 12,
                    ProjectCode = "COMMON",
                },
                new SapInventoryTransferItemsRequests
                {
                    Quantity = 1,
                    UnitPrice = 120000,
                    TaxCode = "SGST18",
                    LocationCode = 2,
                    ItemDescription = "LAND",
                    AccountCode = "_SYS00000000670",
                    SACEntry = 15,
                    ProjectCode = "COMMON",
                },
            ],
        };

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: false);

        payload.DocumentLines.Should().HaveCount(2);
        payload.DocumentLines![0].LineNum.Should().Be(0);
        payload.DocumentLines[1].LineNum.Should().Be(1);
        payload.DocumentLines[0].LocationCode.Should().Be(2);
        payload.DocumentLines[1].LocationCode.Should().Be(2);
        payload.DocumentLines[0].WarehouseCode.Should().BeNull();
        payload.DocumentLines[1].WarehouseCode.Should().BeNull();
    }

    [Test]
    public void Prepare_Create_DoesNotSendDispatchToPartnerUdfs()
    {
        var source = new SapPurchaseRequestsResponse
        {
            CardCode = "S000744",
            UDisId = "C000030",
            UDispachAdd = "Pune plant",
            UShipTo = "Ravi Kumar (9876543210)",
            UContactPerson = "legacy-should-not-send",
        };

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: false);

        payload.UDisId.Should().BeNull();
        payload.UDispachAdd.Should().BeNull();
        payload.UCardCode.Should().BeNull();
        payload.UShipTo.Should().BeNull();
        payload.UContactPerson.Should().BeNull();
        payload.ShipToCode.Should().BeNull();
    }

    [Test]
    public void Prepare_Create_DropsShipToCode_WhenItMatchesDispatchCardCode()
    {
        var source = new SapPurchaseRequestsResponse
        {
            CardCode = "S000744",
            ShipToCode = "C000030",
            UDisId = "C000030",
            UWarehouse = "Store1",
        };

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: false);

        payload.ShipToCode.Should().BeNull();
        payload.UWarehouse.Should().BeNull();
        payload.UDisId.Should().BeNull();
    }

    [Test]
    public void Prepare_Create_OmitsShipToCode()
    {
        var source = new SapPurchaseRequestsResponse
        {
            CardCode = "S000744",
            ShipToCode = "PEARLS METALS",
            UDisId = "C000030",
        };

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: false);

        payload.ShipToCode.Should().BeNull();
    }

    [Test]
    public void Prepare_ItemLine_OmitsMeasureUnit_AndSendsAccountCodeWhenProvided()
    {
        var payload = SapPurchaseRequestPayloadBuilder.Prepare(
            ItemLineDocument(new SapInventoryTransferItemsRequests
            {
                ItemCode = "RM5703813500380",
                Quantity = 10,
                UnitPrice = 100,
                AccountCode = "_SYS00000000677",
                MeasureUnit = "NOS",
                UnitsOfMeasurment = 0.075,
            }),
            isUpdate: false);

        var line = payload.DocumentLines![0];
        line.AccountCode.Should().Be("_SYS00000000677");
        line.MeasureUnit.Should().BeNull();
        line.UnitsOfMeasurment.Should().Be(0.075);
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        json.Should().Contain("AccountCode");
        json.Should().NotContain("MeasureUnit");
    }

    /// <summary>
    /// SAP fills AccountCode on item lines from G/L account determination; sending "" would wipe it.
    /// </summary>
    [Test]
    public void Prepare_ItemLine_OmitsBlankAccountCode()
    {
        var payload = SapPurchaseRequestPayloadBuilder.Prepare(
            ItemLineDocument(new SapInventoryTransferItemsRequests
            {
                ItemCode = "I1",
                Quantity = 1,
                UnitPrice = 1,
                AccountCode = "   ",
            }),
            isUpdate: false);

        payload.DocumentLines![0].AccountCode.Should().BeNull();
        System.Text.Json.JsonSerializer.Serialize(payload).Should().NotContain("AccountCode");
    }

    /// <summary>
    /// Real PO rows store UoMCode "Manual" / UoMEntry -1 with the readable unit in MeasureUnit, so the
    /// unit text must never be sent as UoMCode.
    /// </summary>
    [Test]
    public void Prepare_ItemLine_DoesNotSendUnitTextAsUoMCode()
    {
        var payload = SapPurchaseRequestPayloadBuilder.Prepare(
            ItemLineDocument(new SapInventoryTransferItemsRequests
            {
                ItemCode = "I1",
                Quantity = 1,
                UnitPrice = 1,
                UoMCode = "KGS",
            }),
            isUpdate: false);

        var line = payload.DocumentLines![0];
        line.UoMCode.Should().BeNull();
        line.UoMEntry.Should().BeNull();
        line.MeasureUnit.Should().BeNull();
        System.Text.Json.JsonSerializer.Serialize(payload).Should().NotContain("UoMCode");
    }

    [Test]
    public void Prepare_ItemLine_SendsUoMCodeAndEntryTogether_WhenUoMEntryIsKnown()
    {
        var payload = SapPurchaseRequestPayloadBuilder.Prepare(
            ItemLineDocument(new SapInventoryTransferItemsRequests
            {
                ItemCode = "I1",
                Quantity = 1,
                UnitPrice = 1,
                UoMCode = "BOX",
                UoMEntry = 2,
                MeasureUnit = "BOX",
            }),
            isUpdate: false);

        var line = payload.DocumentLines![0];
        line.UoMCode.Should().Be("BOX");
        line.UoMEntry.Should().Be(2);
        line.MeasureUnit.Should().BeNull();
    }

    [Test]
    public void Prepare_ItemLine_DoesNotSendManualUomEntryOnUpdate()
    {
        var payload = SapPurchaseRequestPayloadBuilder.Prepare(
            ItemLineDocument(new SapInventoryTransferItemsRequests
            {
                LineNum = 0,
                ItemCode = "I1",
                Quantity = 1,
                UnitPrice = 1,
                UoMCode = "KGS",
                UoMEntry = -1,
            }),
            isUpdate: true);

        var line = payload.DocumentLines![0];
        line.UoMCode.Should().BeNull();
        line.UoMEntry.Should().BeNull();
        System.Text.Json.JsonSerializer.Serialize(payload).Should().NotContain("UoMEntry");
        System.Text.Json.JsonSerializer.Serialize(payload).Should().NotContain("UoMCode");
    }

    [Test]
    public void Prepare_ServiceLine_SendsNeitherMeasureUnitNorItemFields()
    {
        var source = new SapPurchaseRequestsResponse
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
                    MeasureUnit = "NOS",
                    UoMCode = "NOS",
                    UoMEntry = 3,
                    UnitsOfMeasurment = 2,
                },
            ],
        };

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: false);

        var line = payload.DocumentLines![0];
        line.AccountCode.Should().Be("600000");
        line.MeasureUnit.Should().BeNull();
        line.UoMCode.Should().BeNull();
        line.UoMEntry.Should().BeNull();
        line.UnitsOfMeasurment.Should().BeNull();
    }

    private static SapPurchaseRequestsResponse ItemLineDocument(SapInventoryTransferItemsRequests line) => new()
    {
        CardCode = "V001",
        DocumentLines = [line],
    };

    [Test]
    public void Prepare_DoesNotSendPoGstTdsOrPaymentTermUdfs()
    {
        var source = new SapPurchaseRequestsResponse
        {
            CardCode = "V001",
            UGstText = "client must not override",
            UTdsText = "client must not override",
            UType1 = "Advance",
            UBasic1 = 20,
            UType3 = "Invoice",
            UGst3 = 100,
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests { ItemCode = "I1", Quantity = 1, UnitPrice = 1 },
            ],
        };

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: false);
        var json = JsonSerializer.Serialize(payload);

        payload.UGstText.Should().BeNull();
        payload.UTdsText.Should().BeNull();
        payload.UGst3.Should().BeNull();
        payload.UGst11.Should().BeNull();
        payload.UBasic1.Should().BeNull();
        json.Should().NotContain("U_GST_");
        json.Should().NotContain("U_TDS_");
        json.Should().NotContain("\"U_B1\"");
        json.Should().NotContain("\"U_G11\"");
    }

    [Test]
    public void MergeDocumentSpecialLinesFromSap_copies_por12_text_onto_the_cached_document()
    {
        var local = new SapPurchaseRequestsResponse
        {
            DocEntry = 4549,
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests { LineNum = 0, ItemCode = "RM1" },
            ],
        };
        var sap = new SapPurchaseRequestsResponse
        {
            DocEntry = 4549,
            DocumentSpecialLines =
            [
                new SapDocumentSpecialLine
                {
                    AfterLineNumber = 0,
                    LineType = "dslt_Text",
                    LineText = "Make as per drawing D-101",
                },
            ],
        };

        SapPurchaseRequestPayloadBuilder.MergeDocumentSpecialLinesFromSap(local, sap);

        local.DocumentSpecialLines.Should().ContainSingle().Which.LineText.Should().Be("Make as per drawing D-101");
        local.DocumentLines![0].FreeText.Should().Be("Make as per drawing D-101");
    }

    [Test]
    public void MergeDocumentSpecialLinesFromSap_does_nothing_when_sap_has_no_special_lines()
    {
        var local = new SapPurchaseRequestsResponse
        {
            DocumentLines = [new SapInventoryTransferItemsRequests { LineNum = 0, ItemCode = "RM1", FreeText = "keep" }],
        };

        SapPurchaseRequestPayloadBuilder.MergeDocumentSpecialLinesFromSap(local, new SapPurchaseRequestsResponse { DocEntry = 1 });

        local.DocumentSpecialLines.Should().BeNull();
        local.DocumentLines![0].FreeText.Should().Be("keep");
    }

    [Test]
    public void Prepare_Create_SetsRequesterAndRequiredDate()
    {
        var source = new SapPurchaseRequestsResponse
        {
            Requester = "manager",
            RequesterName = "Manager",
            DocDueDate = new DateTime(2026, 9, 10),
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests
                {
                    ItemCode = "I1",
                    Quantity = 1,
                    WarehouseCode = "Store1",
                    RequiredDate = new DateTime(2026, 9, 12),
                },
            ],
        };

        var payload = SapPurchaseRequestPayloadBuilder.Prepare(source, isUpdate: false);

        payload.Requester.Should().Be("manager");
        payload.ReqType.Should().Be(Constants.SapPurchaseRequestReqType.User);
        payload.RequiredDate.Should().Be(new DateTime(2026, 9, 10));
        payload.DocumentLines![0].RequiredDate.Should().Be(new DateTime(2026, 9, 12));
        payload.CardCode.Should().BeNull();
    }

    [Test]
    public void Prepare_CopiesHeaderRequiredDateOntoLinesThatOmitIt()
    {
        var due = new DateTime(2026, 9, 10);
        var item = SapPurchaseRequestPayloadBuilder.Prepare(new SapPurchaseRequestsResponse
        {
            DocType = Constants.PurchaseOrderDocType.Document_Item,
            DocDueDate = due,
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests
                {
                    ItemCode = "CO3523639606300000",
                    Quantity = 1,
                    WarehouseCode = "Store1",
                },
            ],
        }, isUpdate: true);

        item.RequiredDate.Should().Be(due);
        item.DocumentLines.Should().ContainSingle().Which.RequiredDate.Should().Be(due);

        var service = SapPurchaseRequestPayloadBuilder.Prepare(new SapPurchaseRequestsResponse
        {
            DocType = Constants.PurchaseOrderDocType.Document_Service,
            DocDueDate = due,
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests
                {
                    AccountCode = "_SYS00000000670",
                    ItemDescription = "LAND",
                    Quantity = 1,
                    LocationCode = 2,
                },
            ],
        }, isUpdate: false);

        service.DocumentLines.Should().ContainSingle().Which.RequiredDate.Should().Be(due);
    }

    [Test]
    public void Prepare_Update_SendsOnlyTheLinesProvidedSoReplaceCollectionsCanDelete()
    {
        var payload = SapPurchaseRequestPayloadBuilder.Prepare(new SapPurchaseRequestsResponse
        {
            DocEntry = 148,
            DocDueDate = new DateTime(2026, 9, 5),
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests
                {
                    LineNum = 0,
                    ItemCode = "CO3523639606300000",
                    Quantity = 5,
                    WarehouseCode = "Store1",
                    LocationCode = 2,
                },
            ],
        }, isUpdate: true);

        payload.DocEntry.Should().Be(148);
        payload.DocumentLines.Should().ContainSingle();
        payload.DocumentLines![0].LineNum.Should().Be(0);
        payload.DocumentLines[0].Quantity.Should().Be(5);
    }
}
