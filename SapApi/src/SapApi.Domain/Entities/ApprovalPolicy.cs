using SapApi.Domain.Entities;
using SapApi.Shared.Enums;

public class ApprovalPolicy
{
    public int Id { get; set; }

    public string CompanyDb { get; set; } = string.Empty;

    public ApprovalDocumentType DocumentType { get; set; }

    public int RequesterUserId { get; set; }

    public bool IsActive { get; set; } = true;

    public ApplicationUser RequesterUser { get; set; }

    public ICollection<ApprovalPolicyApprover> Approvers { get; set; } = [];
    public ICollection<ApprovalPolicyRule> Rules { get; set; } = [];
}