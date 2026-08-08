using System.Security.Claims;
using FluentAssertions;
using SapApi.Shared;

namespace SapApi.Tests;

[TestFixture]
public class ClaimsPrincipalDisplayNameTests
{
    [Test]
    public void GetDisplayName_PrefersFullNameOverLoginUserName()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Name, "pbb.ind07"),
            new Claim(ClaimsPrincipalDisplayName.FullNameClaimType, "Sandeep Bagul"),
        ], "test"));

        ClaimsPrincipalDisplayName.GetDisplayName(user).Should().Be("Sandeep Bagul");
    }

    [Test]
    public void GetDisplayName_FallsBackToUserNameWhenFullNameMissing()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Name, "pbb.ind07"),
        ], "test"));

        ClaimsPrincipalDisplayName.GetDisplayName(user).Should().Be("pbb.ind07");
    }

    [Test]
    public void GetDisplayName_DoesNotUseNumericUserId()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "42"),
        ], "test"));

        ClaimsPrincipalDisplayName.GetDisplayName(user).Should().BeEmpty();
    }
}
