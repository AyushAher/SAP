using FluentAssertions;
using SapApi.Shared;

namespace SapApi.Tests;

[TestFixture]
public class PoDispatchWarehousesTests
{
    [TestCase(1, "Store1", true)] // Privilege Biksons — Factory
    [TestCase(1, "Store5", true)] // Privilege Biksons — Office
    [TestCase(1, "PBPL(S)", false)] // Privilege Biksons — Customer Loc ships from BP address
    [TestCase(1, "SUBCON", false)] // Privilege Biksons — SubContractor Loc ships from BP address
    [TestCase(3, "Store3", true)] // S M Projects — Office
    [TestCase(3, "PBPL(S)", false)] // Not one of S M Projects' warehouses
    [TestCase(4, "Store4", true)] // De Design — Office
    [TestCase(5, "PEPL(P)", true)] // Privilege Energex — Factory
    [TestCase(5, "Store9", true)] // Privilege Energex — Office
    [TestCase(5, "PEPL(S)", false)] // Privilege Energex — Customer Loc ships from BP address
    public void IsFactoryOrOffice_matches_the_branchs_own_factory_or_office_warehouse(
        int bplId, string warehouseCode, bool expected)
    {
        Constants.PoDispatchWarehouses.IsFactoryOrOffice(bplId, warehouseCode).Should().Be(expected);
    }

    [Test]
    public void IsFactoryOrOffice_is_false_for_an_unmapped_branch_or_null_warehouse()
    {
        Constants.PoDispatchWarehouses.IsFactoryOrOffice(99, "Store1").Should().BeFalse();
        Constants.PoDispatchWarehouses.IsFactoryOrOffice(1, null).Should().BeFalse();
        Constants.PoDispatchWarehouses.IsFactoryOrOffice(null, "Store1").Should().BeFalse();
    }
}
