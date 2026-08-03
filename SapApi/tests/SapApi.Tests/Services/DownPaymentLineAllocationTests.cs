using FluentAssertions;
using SapApi.Infrastructure.Services;
using SapApi.Shared;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services;

public class DownPaymentLineAllocationTests
{
    [Test]
    public void BuildDownPaymentDocumentLines_AllocatesLineTotalsToMatchAmount()
    {
        var po = new SapPurchaseOrdersResponse
        {
            DocEntry = 6412,
            Project = "PB/R&M/25262051",
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests
                {
                    ItemCode = "A",
                    LineNum = 0,
                    LineTotal = 10000,
                    WarehouseCode = "Store1",
                },
                new SapInventoryTransferItemsRequests
                {
                    ItemCode = "B",
                    LineNum = 1,
                    LineTotal = 11712,
                    WarehouseCode = "Store1",
                },
            ],
        };

        var lines = StageWisePaymentService.BuildDownPaymentDocumentLines(
            po, po.DocumentLines!, amount: 18400, isGst: false);

        lines.Should().HaveCount(2);
        lines.Sum(l => l.LineTotal ?? 0).Should().Be(18400);
        lines.Should().OnlyContain(l =>
            Equals(l.BaseType, 22)
            && l.BaseEntry == 6412
            && l.WTLiable == Constants.SapBoolean.SapTrue
            && l.TaxLiable == Constants.SapBoolean.SapFalse);
        lines[0].LineTotal.Should().Be(Math.Round(18400 * 10000 / 21712.0, 2));
        lines[1].LineTotal.Should().Be(Math.Round(18400 - lines[0].LineTotal!.Value, 2));
    }

    [Test]
    public void BuildDownPaymentDocumentLines_GstLines_AreNotWtLiable()
    {
        var po = new SapPurchaseOrdersResponse
        {
            DocEntry = 1,
            DocumentLines =
            [
                new SapInventoryTransferItemsRequests { ItemCode = "A", LineNum = 0, LineTotal = 100 },
            ],
        };

        var lines = StageWisePaymentService.BuildDownPaymentDocumentLines(
            po, po.DocumentLines!, amount: 50, isGst: true);

        lines.Should().ContainSingle();
        lines[0].LineTotal.Should().Be(50);
        lines[0].WTLiable.Should().Be(Constants.SapBoolean.SapFalse);
    }
}
