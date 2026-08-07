using FluentAssertions;
using SapApi.Infrastructure.Services;
using SapApi.Shared;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services;

public class DownPaymentLineAllocationTests
{
    [Test]
    public void ApplyPostingDate_SetsDocDateAndTaxDate()
    {
        var request = new SapPurchaseDownPaymentRequest();
        var postingDate = new DateTime(2026, 8, 6, 15, 30, 0, DateTimeKind.Utc);

        StageWisePaymentService.ApplyPostingDate(request, postingDate);

        request.DocDate.Should().Be(new DateTime(2026, 8, 6));
        request.TaxDate.Should().Be(new DateTime(2026, 8, 6));
    }

    [Test]
    public void ApplyPostingDate_PaymentDate_PreferredForDocDate()
    {
        var request = new SapPurchaseDownPaymentRequest();
        var postingDate = new DateTime(2026, 8, 6);
        var paymentDate = new DateTime(2026, 8, 10);

        StageWisePaymentService.ApplyPostingDate(request, postingDate, paymentDate);

        request.DocDate.Should().Be(paymentDate);
        request.TaxDate.Should().Be(postingDate);
    }

    [Test]
    public void ApplyPostingDate_Null_LeavesDatesUnset()
    {
        var request = new SapPurchaseDownPaymentRequest();

        StageWisePaymentService.ApplyPostingDate(request, null);

        request.DocDate.Should().BeNull();
        request.TaxDate.Should().BeNull();
    }

    [Test]
    public void ApplyVendorPaymentDates_MapsPaymentDateToDocDate()
    {
        var request = new SapVendorPaymentRequests
        {
            CardCode = "V001",
            TransferSum = "100",
            TransferReference = "R1",
            CounterReference = "R1",
            TransferAccount = "_SYS00000000980",
        };
        var paymentDate = new DateTime(2026, 8, 10);
        var postingDate = new DateTime(2026, 8, 6);

        StageWisePaymentService.ApplyVendorPaymentDates(request, paymentDate, postingDate);

        request.DocDate.Should().Be(paymentDate);
        request.PostingDate.Should().Be(postingDate);
        request.TransferDate.Should().Be(paymentDate);
        request.DocDueDate.Should().Be(paymentDate);
    }

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
