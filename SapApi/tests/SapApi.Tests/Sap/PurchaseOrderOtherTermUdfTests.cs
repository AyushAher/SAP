using FluentAssertions;
using SapApi.Shared.Models;

namespace SapApi.Tests.Sap;

[TestFixture]
public class PurchaseOrderOtherTermUdfTests
{
    [Test]
    public void IsPackingForwarding_MatchesNameAndDescription()
    {
        PurchaseOrderOtherTermUdf.IsPackingForwarding("PAC_FOR", null).Should().BeTrue();
        PurchaseOrderOtherTermUdf.IsPackingForwarding("PACK_FOR", null).Should().BeTrue();
        PurchaseOrderOtherTermUdf.IsPackingForwarding("PCKFWD", "anything").Should().BeTrue();
        PurchaseOrderOtherTermUdf.IsPackingForwarding("X", "Packing & Forwarding").Should().BeTrue();
        PurchaseOrderOtherTermUdf.IsPackingForwarding("X", "P&F").Should().BeTrue();
        PurchaseOrderOtherTermUdf.IsPackingForwarding("X", "Loading").Should().BeFalse();
        PurchaseOrderOtherTermUdf.IsPackingForwarding("TRANS", "Transportation").Should().BeFalse();
    }

    [Test]
    public void IsTcDispatchAddress_RequiresTcAndDoesNotMatchDispatchAddressAlone()
    {
        PurchaseOrderOtherTermUdf.IsTcDispatchAddress("TCDISADD", null).Should().BeTrue();
        PurchaseOrderOtherTermUdf.IsTcDispatchAddress("TC_DISP", null).Should().BeTrue();
        PurchaseOrderOtherTermUdf.IsTcDispatchAddress("X", "TC Dispatch Address").Should().BeTrue();
        PurchaseOrderOtherTermUdf.IsTcDispatchAddress("X", "Test Certificate Dispatch").Should().BeTrue();
        PurchaseOrderOtherTermUdf.IsTcDispatchAddress("DispachAdd", "Dispatch Address").Should().BeFalse();
        PurchaseOrderOtherTermUdf.IsTcDispatchAddress("SHIPTO", "Ship To Contact").Should().BeFalse();
    }

    [Test]
    public void IsWritableAdditionalKey_AllowsUdfScalarsAndRejectsMetadata()
    {
        PurchaseOrderOtherTermUdf.IsWritableAdditionalKey("U_PACK_FOR").Should().BeTrue();
        PurchaseOrderOtherTermUdf.IsWritableAdditionalKey("odata.etag").Should().BeFalse();
        PurchaseOrderOtherTermUdf.IsWritableAdditionalKey("DocEntry").Should().BeFalse();
    }
}
