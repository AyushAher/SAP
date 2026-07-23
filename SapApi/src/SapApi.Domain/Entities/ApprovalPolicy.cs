using SapApi.Domain.Entities;
using SapApi.Shared.Enums;

public class ApprovalPolicy
{
    public int Id { get; set; }

    public string CompanyDb { get; set; } = string.Empty;

    public ApprovalDocumentType DocumentType { get; set; }

    /// <summary>
    /// Whether this policy applies to a single user or a user group (requester side only).
    /// </summary>
    public ApprovalRequesterType RequesterType { get; set; } = ApprovalRequesterType.User;

    /// <summary>
    /// Set when <see cref="RequesterType"/> is <see cref="ApprovalRequesterType.User"/>.
    /// </summary>
    public int? RequesterUserId { get; set; }

    /// <summary>
    /// Set when <see cref="RequesterType"/> is <see cref="ApprovalRequesterType.Group"/>.
    /// </summary>
    public int? RequesterGroupId { get; set; }

    public bool IsActive { get; set; } = true;

    public ApplicationUser? RequesterUser { get; set; }

    public UserGroup? RequesterGroup { get; set; }

    public ICollection<ApprovalPolicyApprover> Approvers { get; set; } = [];
    public ICollection<ApprovalPolicyRule> Rules { get; set; } = [];
}
