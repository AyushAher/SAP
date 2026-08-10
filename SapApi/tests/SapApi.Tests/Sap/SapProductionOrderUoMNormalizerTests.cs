using System.Text.Json;
using FluentAssertions;
using SapApi.Shared.Sap;

namespace SapApi.Tests.Sap;

[TestFixture]
public class SapProductionOrderUoMNormalizerTests
{
    [Test]
    public void NormalizeUoMCode_DropsInventoryUomName()
    {
        SapProductionOrderUoMNormalizer.NormalizeUoMCode("KG").Should().BeNull();
        SapProductionOrderUoMNormalizer.NormalizeUoMCode("NOS").Should().BeNull();
    }

    [Test]
    public void NormalizeUoMCode_KeepsWholeNumbers()
    {
        SapProductionOrderUoMNormalizer.NormalizeUoMCode(12).Should().Be(12);
        SapProductionOrderUoMNormalizer.NormalizeUoMCode("7").Should().Be(7);
        SapProductionOrderUoMNormalizer.NormalizeUoMCode(3L).Should().Be(3);
    }

    [Test]
    public void NormalizeUoMCode_HandlesJsonElement()
    {
        using var numericDoc = JsonDocument.Parse("42");
        SapProductionOrderUoMNormalizer.NormalizeUoMCode(numericDoc.RootElement).Should().Be(42);

        using var stringDoc = JsonDocument.Parse("\"KG\"");
        SapProductionOrderUoMNormalizer.NormalizeUoMCode(stringDoc.RootElement).Should().BeNull();

        using var numericStringDoc = JsonDocument.Parse("\"15\"");
        SapProductionOrderUoMNormalizer.NormalizeUoMCode(numericStringDoc.RootElement).Should().Be(15);
    }

    [Test]
    public void NormalizeUoMCode_NullOrEmpty_ReturnsNull()
    {
        SapProductionOrderUoMNormalizer.NormalizeUoMCode(null).Should().BeNull();
        SapProductionOrderUoMNormalizer.NormalizeUoMCode("").Should().BeNull();
        SapProductionOrderUoMNormalizer.NormalizeUoMCode("   ").Should().BeNull();
    }
}
