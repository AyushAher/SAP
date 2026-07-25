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
}
