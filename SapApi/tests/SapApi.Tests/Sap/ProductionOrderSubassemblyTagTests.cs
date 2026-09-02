using FluentAssertions;
using SapApi.Shared.Sap;

namespace SapApi.Tests.Sap;

[TestFixture]
public class ProductionOrderSubassemblyTagTests
{
    [Test]
    public void ParentDocumentNumber_strips_the_sequence()
    {
        ProductionOrderSubassemblyTag.ParentDocumentNumber("13/2").Should().Be("13");
        ProductionOrderSubassemblyTag.ParentDocumentNumber("13-2").Should().Be("13");
        ProductionOrderSubassemblyTag.ParentDocumentNumber("13").Should().Be("13");
        ProductionOrderSubassemblyTag.ParentDocumentNumber("  ").Should().BeNull();
    }

    [Test]
    public void ToSapTag_replaces_the_slash_SAP_rejects()
    {
        ProductionOrderSubassemblyTag.ToSapTag("8/1").Should().Be("8-1");
        ProductionOrderSubassemblyTag.ToSapTag("8-1").Should().Be("8-1");
        ProductionOrderSubassemblyTag.ToSapTag("test").Should().Be("test");
    }

    [Test]
    public void IsTagged_is_true_only_for_parent_and_sequence()
    {
        ProductionOrderSubassemblyTag.IsTagged(null).Should().BeFalse();
        ProductionOrderSubassemblyTag.IsTagged("").Should().BeFalse();
        ProductionOrderSubassemblyTag.IsTagged("test").Should().BeFalse();
        ProductionOrderSubassemblyTag.IsTagged("8").Should().BeFalse();
        ProductionOrderSubassemblyTag.IsTagged("10/1").Should().BeTrue();
        ProductionOrderSubassemblyTag.IsTagged("10-1").Should().BeTrue();
    }

    [Test]
    public void EqualsTag_treats_slash_and_hyphen_as_the_same_tag()
    {
        ProductionOrderSubassemblyTag.EqualsTag("10/1", "10-1").Should().BeTrue();
        ProductionOrderSubassemblyTag.EqualsTag("10/1", "10/1").Should().BeTrue();
        ProductionOrderSubassemblyTag.EqualsTag("10/1", "10/2").Should().BeFalse();
        ProductionOrderSubassemblyTag.EqualsTag("0-1", "36/1").Should().BeTrue();
        ProductionOrderSubassemblyTag.EqualsTag("0/1", "10-1").Should().BeTrue();
        ProductionOrderSubassemblyTag.EqualsTag("0-1", "10-2").Should().BeFalse();
        ProductionOrderSubassemblyTag.EqualsTag("10/1", "11/1").Should().BeFalse();
        ProductionOrderSubassemblyTag.EqualsTag(null, "10/1").Should().BeFalse();
    }

    [Test]
    public void WithParent_keeps_the_sequence_on_a_new_document_number()
    {
        ProductionOrderSubassemblyTag.WithParent("0/1", "35").Should().Be("35/1");
        ProductionOrderSubassemblyTag.WithParent("0-2", "35").Should().Be("35/2");
        ProductionOrderSubassemblyTag.ToSapTag(ProductionOrderSubassemblyTag.WithParent("0/1", "35"))
            .Should().Be("35-1");
    }
}
