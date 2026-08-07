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
    public void FormatDownPaymentRemarkLabel_UsesStageWhenDescMissing()
    {
        var term = new PaymentTermsUdf { Id = 3, Stage = "Against inspection", Basic = 10, Gst = 5 };
        StageWisePaymentCalculations.FormatDownPaymentRemarkLabel(term)
            .Should().Be("Against inspection");
    }

    [Test]
    public void FormatDownPaymentRemarkLabel_DoesNotUseBasicGstPercentageFallback()
    {
        var term = new PaymentTermsUdf { Id = 3, Basic = 30, Gst = 18 };
        StageWisePaymentCalculations.FormatDownPaymentRemarkLabel(term)
            .Should().Be("Down Payment");
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
    public void ResolveBatchDownPaymentRemarkLabel_MultipleLinesDifferentTerms_DoesNotConcatenate()
    {
        var lines = new List<StageWisePaymentBatchLineRequest>
        {
            new() { PaymentTermsTypes = [1], Amount = 500 },
            new() { PaymentTermsTypes = [2], Amount = 500 },
        };

        StageWisePaymentCalculations.ResolveBatchDownPaymentRemarkLabel(PaymentTerms, lines)
            .Should().Be("Batch down payment");
    }

    [Test]
    public void BuildDownPayment_FormatsSingleTermWithPoNumber()
    {
        Constants.PaymentRemarks.BuildDownPayment("30% Advance against PO", "1234")
            .Should().Be("30% Advance against PO. Based on Purchase Order no. 1234");
    }
}
