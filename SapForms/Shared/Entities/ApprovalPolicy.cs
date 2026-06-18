using Shared.Entities;
using Shared.Enums;

public class ApprovalPolicy
{
    public int Id { get; set; }

    public ApprovalDocumentType DocumentType { get; set; }

    public int RequesterUserId { get; set; }

    public bool IsActive { get; set; } = true;

    public ApplicationUser RequesterUser { get; set; }

    public ICollection<ApprovalPolicyApprover> Approvers { get; set; } = [];
    public ICollection<ApprovalPolicyRule> Rules { get; set; } = [];
}