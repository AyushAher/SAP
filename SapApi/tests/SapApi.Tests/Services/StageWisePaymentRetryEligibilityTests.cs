using FluentAssertions;
using SapApi.Domain.Entities;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Enums;

namespace SapApi.Tests.Services;

[TestFixture]
public class StageWisePaymentRetryEligibilityTests
{
    [Test]
    public void ResolveRetrySap_WhenApprovedWithoutSapDoc_IsEligible()
    {
        var payment = new StageWisePayment
        {
            Id = 36,
            Status = StageWisePaymentStatus.PendingApproval,
            ApprovalRequestId = "16",
            PaymentDocEntry = null,
        };
        var approvals = new Dictionary<int, ApprovalRequest>
        {
            [16] = new ApprovalRequest
            {
                Id = 16,
                IsApproved = true,
                OverallStatus = ApprovalStatus.Approved,
                DocumentType = ApprovalDocumentType.Payments,
                RequestBody = "{\"CardCode\":\"V1\"}",
                SapResponseDocEntry = null,
            },
        };

        var (canRetry, requestId) = StageWisePaymentPageService.ResolveRetrySap(payment, approvals);

        canRetry.Should().BeTrue();
        requestId.Should().Be(16);
        StageWisePaymentPageService.MapStatus(payment, approvals).Should().Be("SAP Posting Pending");
    }

    [Test]
    public void ResolveRetrySap_WhenStillPendingApproval_IsNotEligible()
    {
        var payment = new StageWisePayment
        {
            Status = StageWisePaymentStatus.PendingApproval,
            ApprovalRequestId = "16",
        };
        var approvals = new Dictionary<int, ApprovalRequest>
        {
            [16] = new ApprovalRequest
            {
                Id = 16,
                IsApproved = false,
                OverallStatus = ApprovalStatus.Pending,
                DocumentType = ApprovalDocumentType.Payments,
                RequestBody = "{\"CardCode\":\"V1\"}",
            },
        };

        var (canRetry, requestId) = StageWisePaymentPageService.ResolveRetrySap(payment, approvals);

        canRetry.Should().BeFalse();
        requestId.Should().BeNull();
        StageWisePaymentPageService.MapStatus(payment, approvals).Should().Be("Approval Pending");
    }

    [Test]
    public void ResolveRetrySap_WhenFailedWithoutSapDoc_IsEligible()
    {
        var payment = new StageWisePayment
        {
            Status = StageWisePaymentStatus.PendingApproval,
            ApprovalRequestId = "16",
            PaymentDocEntry = null,
        };
        var approvals = new Dictionary<int, ApprovalRequest>
        {
            [16] = new ApprovalRequest
            {
                Id = 16,
                IsApproved = true,
                OverallStatus = ApprovalStatus.Failed,
                DocumentType = ApprovalDocumentType.Payments,
                RequestBody = "{\"CardCode\":\"V1\"}",
                FailureReason = "SAP timeout",
            },
        };

        var (canRetry, requestId) = StageWisePaymentPageService.ResolveRetrySap(payment, approvals);

        canRetry.Should().BeTrue();
        requestId.Should().Be(16);
    }

    [Test]
    public void ResolveRetrySap_WhenSapDocExists_IsNotEligible()
    {
        var payment = new StageWisePayment
        {
            Status = StageWisePaymentStatus.Approved,
            ApprovalRequestId = "16",
            PaymentDocEntry = "999",
        };
        var approvals = new Dictionary<int, ApprovalRequest>
        {
            [16] = new ApprovalRequest
            {
                Id = 16,
                IsApproved = true,
                OverallStatus = ApprovalStatus.Approved,
                DocumentType = ApprovalDocumentType.Payments,
                RequestBody = "{\"CardCode\":\"V1\"}",
                SapResponseDocEntry = "999",
            },
        };

        var (canRetry, _) = StageWisePaymentPageService.ResolveRetrySap(payment, approvals);

        canRetry.Should().BeFalse();
        StageWisePaymentPageService.MapStatus(payment, approvals).Should().Be("Approved");
    }
}
