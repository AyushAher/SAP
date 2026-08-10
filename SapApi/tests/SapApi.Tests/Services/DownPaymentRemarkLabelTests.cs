using FluentAssertions;
using SapApi.Infrastructure.Services;
using SapApi.Shared;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Services;

public class DownPaymentRemarkLabelTests
{
    private static readonly List<PaymentTermsUdf> PaymentTerms =
    [
        new() { Id = 1, Desc = "30% Advance against PO", Basic = 30, Gst = 30, Type = "Advance" },
        new() { Id = 2, Desc = "70% Against delivery", Basic = 70, Gst = 70, Type = "Delivery" },
    ];

    [Test]
    public void FormatDownPaymentRemarkLabel_PrefersDescOverBasicGstFallback()
    {
        StageWisePaymentCalculations.FormatDownPaymentRemarkLabel(PaymentTerms[0])
            .Should().Be("30% Advance against PO");
    }

    [Test]
    public void FormatDownPaymentRemarkLabel_UsesStageWhenDescAndPercentsMissing()
    {
        var term = new PaymentTermsUdf { Id = 3, Stage = "Against inspection" };
        StageWisePaymentCalculations.FormatDownPaymentRemarkLabel(term)
            .Should().Be("Against inspection");
    }

    [Test]
    public void FormatDownPaymentRemarkLabel_SynthesizesBasicAndGstAgainstTypeWhenDescMissing()
    {
        var term = new PaymentTermsUdf { Id = 3, Basic = 100, Type = "Proforma" };
        StageWisePaymentCalculations.FormatDownPaymentRemarkLabel(term)
            .Should().Be("100% Basic Against Proforma");

        var gst = new PaymentTermsUdf { Id = 11, Gst = 100, Type = "GstProforma" };
        StageWisePaymentCalculations.FormatDownPaymentRemarkLabel(gst)
            .Should().Be("100% GST Against Proforma");
    }

    [Test]
    public void FormatDownPaymentRemarkLabel_DoesNotUseBasicGstPercentageDropDownFallback()
    {
        var term = new PaymentTermsUdf { Id = 3, Basic = 30, Gst = 18 };
        StageWisePaymentCalculations.FormatDownPaymentRemarkLabel(term)
            .Should().Be("30% Basic Against Proforma, 18% GST Against Proforma");
        term.DropDownValue().Should().Be("Basic 30% & GST 18%");
    }

    [Test]
    public void ResolveBatchDownPaymentRemarkLabel_SingleLine_UsesThatTermOnly()
    {
        var lines = new List<StageWisePaymentBatchLineRequest>
        {
            new() { PaymentTermsTypes = [1], Amount = 1000 },
        };

        StageWisePaymentCalculations.ResolveBatchDownPaymentRemarkLabel(PaymentTerms, lines)
            .Should().Be("30% Advance against PO");
    }

    [Test]
    public void ResolveBatchDownPaymentRemarkLabel_MultipleLinesDifferentTerms_JoinsDistinctDescs()
    {
        var lines = new List<StageWisePaymentBatchLineRequest>
        {
            new() { PaymentTermsTypes = [1], Amount = 500 },
            new() { PaymentTermsTypes = [2], Amount = 500 },
        };

        StageWisePaymentCalculations.ResolveBatchDownPaymentRemarkLabel(PaymentTerms, lines)
            .Should().Be("30% Advance against PO, 70% Against delivery");
    }

    [Test]
    public void BuildDownPayment_FormatsSingleTermWithPrefixedPoNumber()
    {
        Constants.PaymentRemarks.BuildDownPayment("30% Advance against PO", bplId: 1, "1234")
            .Should().Be("30% Advance against PO. Based on Purchase Order no. PB/PO/1234");
    }

    [Test]
    public void BuildDownPayment_WithoutBranch_UsesPoSlashPrefix()
    {
        Constants.PaymentRemarks.BuildDownPayment("30% Advance against PO", "1234")
            .Should().Be("30% Advance against PO. Based on Purchase Order no. PO/1234");
    }

    [Test]
    public void PdfJournalRemarks_Downpayment_UsesPaymentTermsAndPrefixedPoNumber()
    {
        StageWisePaymentPdfBuilder.ResolveJournalRemarks(
                paymentTypeLabel: "Downpayment Request",
                isBatchDown: true,
                paymentTermText: "80% Basic Against Proforma",
                bplId: 1,
                poDocNum: "262711481",
                userRemark: "ignored for down payment")
            .Should().Be("80% Basic Against Proforma. Based on Purchase Order no. PB/PO/262711481");
    }
}
