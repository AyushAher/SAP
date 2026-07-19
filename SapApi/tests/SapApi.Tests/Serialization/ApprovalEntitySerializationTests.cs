using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using SapApi.Domain.Entities;
using SapApi.Shared.Enums;

namespace SapApi.Tests.Serialization;

/// <summary>
/// Regression coverage for the production incident where GET /api/approvals/pending/list threw
/// System.Text.Json.JsonException: "A possible object cycle was detected" because EF Core's
/// navigation fixup wires up ApplicationUser's inverse collections (and ApprovalRequest/UserApproval
/// back-references) even under AsNoTracking whenever the same user or parent appears more than once
/// in a single query's result graph.
/// </summary>
[TestFixture]
public class ApprovalEntitySerializationTests
{
    private static JsonSerializerOptions Options => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    [Test]
    public void Serializing_MultipleRequestsFromSameRequester_WithEfFixup_DoesNotThrow()
    {
        var requester = new ApplicationUser { Id = 1, UserName = "requester@test.com", FullName = "Requester" };
        var approver = new ApplicationUser { Id = 2, UserName = "approver@test.com", FullName = "Approver" };

        var requestA = new ApprovalRequest { Id = 100, RequesterUserId = 1, RequesterUser = requester, DocumentType = ApprovalDocumentType.PurchaseOrder };
        var requestB = new ApprovalRequest { Id = 101, RequesterUserId = 1, RequesterUser = requester, DocumentType = ApprovalDocumentType.Payments };

        requestA.UserApprovals = [new UserApproval { Id = 1, ApprovalRequestId = 100, ApprovalRequest = requestA, UserId = 2, User = approver, Priority = 1 }];
        requestB.UserApprovals = [new UserApproval { Id = 2, ApprovalRequestId = 101, ApprovalRequest = requestB, UserId = 2, User = approver, Priority = 1 }];

        // Simulate the exact EF Core "fixup" that caused the crash: the same tracked ApplicationUser
        // instance gets its inverse collection populated with every ApprovalRequest seen in the query.
        requester.ApprovalRequest = [requestA, requestB];
        approver.ApprovalRequest = [];

        var act = () => JsonSerializer.Serialize(new List<ApprovalRequest> { requestA, requestB }, Options);

        act.Should().NotThrow();
    }

    [Test]
    public void Serializing_ApplicationUser_NeverEmitsBackReferenceCollections()
    {
        var requester = new ApplicationUser { Id = 1, UserName = "requester@test.com", FullName = "Requester" };
        var request = new ApprovalRequest { Id = 100, RequesterUserId = 1, RequesterUser = requester };
        requester.ApprovalRequest = [request];

        var json = JsonSerializer.Serialize(requester, Options);

        json.Should().NotContain("approvalRequest");
        json.Should().NotContain("policy");
        json.Should().NotContain("policyApprover");
    }

    [Test]
    public void Serializing_UserApproval_NeverEmitsParentBackReference()
    {
        var request = new ApprovalRequest { Id = 100 };
        var approval = new UserApproval { Id = 1, ApprovalRequestId = 100, ApprovalRequest = request };

        var json = JsonSerializer.Serialize(approval, Options);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("approvalRequest", out _).Should().BeFalse();
    }
}
