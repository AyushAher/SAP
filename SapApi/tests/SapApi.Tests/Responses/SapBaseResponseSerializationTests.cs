using System.Text.Json;
using FluentAssertions;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Tests.Responses;

[TestFixture]
public class SapBaseResponseSerializationTests
{
    [Test]
    public void SerializesPendingApprovalFlags_WhenTrue()
    {
        var response = new SapPurchaseOrdersResponse
        {
            PendingApproval = true,
            PendingApprovalRequestId = 42,
        };

        var json = JsonSerializer.Serialize(response);

        json.Should().Contain("\"pendingApproval\":true");
        json.Should().Contain("\"pendingApprovalRequestId\":42");
    }

    [Test]
    public void OmitsPendingApproval_WhenFalse()
    {
        var response = new SapPurchaseOrdersResponse
        {
            PendingApproval = false,
            PendingApprovalRequestId = null,
            DocEntry = 1,
        };

        var json = JsonSerializer.Serialize(response);

        json.Should().NotContain("pendingApproval");
        json.Should().NotContain("pendingApprovalRequestId");
    }
}
