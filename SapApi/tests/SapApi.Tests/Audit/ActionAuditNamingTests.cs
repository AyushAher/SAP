using FluentAssertions;
using SapApi.Infrastructure.Audit;

namespace SapApi.Tests.Audit;

[TestFixture]
public class ActionAuditNamingTests
{
    [TestCase("GET", "/api/purchase-orders/list", false)]
    [TestCase("POST", "/api/purchase-orders/list", false)]
    [TestCase("POST", "/api/purchase-orders", true)]
    [TestCase("PUT", "/api/purchase-orders/42", true)]
    [TestCase("POST", "/api/auth/login", true)]
    [TestCase("GET", "/api/auth/public-key", false)]
    [TestCase("GET", "/health", false)]
    public void ShouldAudit_matches_expected_requests(string method, string path, bool expected) =>
        ActionAuditNaming.ShouldAudit(method, path).Should().Be(expected);

    [TestCase("POST", "/api/purchase-orders", "PurchaseOrder.Create")]
    [TestCase("PUT", "/api/purchase-orders/42", "PurchaseOrder.Update")]
    [TestCase("POST", "/api/auth/login", "Auth.Login")]
    [TestCase("POST", "/api/stage-wise-payments/9/cancel", "StageWisePayment.Cancel")]
    public void BuildActionLabel_returns_readable_action(string method, string path, string expected) =>
        ActionAuditNaming.BuildActionLabel(method, path).Should().Be(expected);
}
