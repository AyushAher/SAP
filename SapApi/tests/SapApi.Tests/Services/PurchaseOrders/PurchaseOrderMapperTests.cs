using FluentAssertions;
using SapApi.Infrastructure.Services.PurchaseOrders;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services.PurchaseOrders;

[TestFixture]
public class PurchaseOrderMapperTests
{
    [Test]
    public void ToSapResponse_roundtrips_payment_terms_and_lines()
    {
        var sap = new SapPurchaseOrdersResponse
        {
            DocEntry = 101,
            DocNum = 5001,
            CardCode = "V001",
            CardName = "Vendor",
            DocTotal = 1100,
            VatSum = 100,
            UBasic1 = 50,
            UGst1 = 18,
            UDes1 = "Advance",
            UStage1 = "S1",
            UType1 = "Basic",
            DocumentLines =
            [
                new()
                {
                    LineNum = 0,
                    ItemCode = "ITM1",
                    Quantity = 2,
                    UnitPrice = 500,
                    UnitsOfMeasurment = 1,
                    UseBaseUnits = "tYES",
                },
            ],
        };

        var entity = new Domain.Entities.PurchaseOrder
        {
            Id = 1,
            CompanyDb = "TEST",
            DocEntry = 101,
            CreatedOn = DateTime.UtcNow,
        };
        PurchaseOrderMapper.ApplyHeader(entity, sap, DateTime.UtcNow);
        entity.Lines = PurchaseOrderMapper.MapLines(entity.Id, sap.DocumentLines);
        entity.PaymentTerms = PurchaseOrderMapper.MapPaymentTerms(entity.Id, sap);

        var mapped = PurchaseOrderMapper.ToSapResponse(entity, includeLines: true);

        mapped.DocEntry.Should().Be(101);
        mapped.CardCode.Should().Be("V001");
        mapped.UBasic1.Should().Be(50);
        mapped.UGst1.Should().Be(18);
        mapped.UDes1.Should().Be("Advance");
        mapped.DocumentLines.Should().ContainSingle();
        mapped.DocumentLines![0].ItemCode.Should().Be("ITM1");
        mapped.DocumentLines[0].UseBaseUnits.Should().Be("tYES");
        entity.PaymentTerms.Should().ContainSingle();
        entity.PaymentTerms.First().Slot.Should().Be(1);
    }
}
