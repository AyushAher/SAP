using FluentAssertions;
using SapApi.Shared.Models;

namespace SapApi.Tests.Services;

[TestFixture]
public class PaymentTermTypeOptionsTests
{
    [Test]
    public void MergeWithExtras_AppendsMissingAppExtras()
    {
        var sap = new List<PaymentTermTypeOption>
        {
            new() { Value = "Advance", Description = "As Advance" },
            new() { Value = "Proforma", Description = "Against Proforma" },
        };

        var merged = PaymentTermTypeOptions.MergeWithExtras(sap);

        merged.Select(x => x.Value).Should().Equal(
            "Advance", "Proforma", "GstProforma", "TaxInvoice");
        merged.Single(x => x.Value == "GstProforma").Description.Should().Be("GST against Proforma Invoice");
        merged.Single(x => x.Value == "TaxInvoice").Description.Should().Be("Against Tax Invoice");
    }

    [Test]
    public void MergeWithExtras_DoesNotDuplicateExistingExtras()
    {
        var sap = new List<PaymentTermTypeOption>
        {
            new() { Value = "Advance", Description = "As Advance" },
            new() { Value = "GstProforma", Description = "Custom GST label" },
            new() { Value = "TaxInvoice", Description = "Custom tax label" },
        };

        var merged = PaymentTermTypeOptions.MergeWithExtras(sap);

        merged.Count(x => x.Value.Equals("GstProforma", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
        merged.Single(x => x.Value == "GstProforma").Description.Should().Be("Custom GST label");
    }

    [Test]
    public void FallbackWithExtras_IncludesSapDefaultsAndExtras()
    {
        var fallback = PaymentTermTypeOptions.FallbackWithExtras();
        fallback.Select(x => x.Value).Should().Equal(
            "Advance", "Proforma", "Invoice", "Retention", "GstProforma", "TaxInvoice");
    }

    [Test]
    public void IsGstMappedType_RecognizesGstTypes()
    {
        PaymentTermTypeOptions.IsGstMappedType("GstProforma").Should().BeTrue();
        PaymentTermTypeOptions.IsGstMappedType("taxinvoice").Should().BeTrue();
        PaymentTermTypeOptions.IsGstMappedType("Advance").Should().BeFalse();
        PaymentTermTypeOptions.IsGstMappedType(null).Should().BeFalse();
    }
}
