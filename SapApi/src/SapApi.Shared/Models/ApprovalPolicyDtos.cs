using SapApi.Shared.Enums;

namespace SapApi.Shared.Models;

public class ApprovalPolicyDto
{
    public int Id { get; set; }
    public ApprovalDocumentType DocumentType { get; set; }
    public ApprovalRequesterType RequesterType { get; set; }
    public int? RequesterUserId { get; set; }
    public string? RequesterName { get; set; }
    public int? RequesterGroupId { get; set; }
    public string? RequesterGroupName { get; set; }
    public bool IsActive { get; set; }
    public List<ApprovalPolicyApproverDto> Approvers { get; set; } = [];
    public List<ApprovalPolicyRuleDto> Rules { get; set; } = [];
}

public class ApprovalPolicyApproverDto
{
    public int ApproverUserId { get; set; }
    public int Priority { get; set; }
}

public class ApprovalPolicyRuleDto
{
    public string FieldName { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class UpsertApprovalPolicyRequest
{
    public ApprovalDocumentType DocumentType { get; set; }
    public ApprovalRequesterType RequesterType { get; set; } = ApprovalRequesterType.User;
    public int? RequesterUserId { get; set; }
    public int? RequesterGroupId { get; set; }
    public List<ApprovalPolicyApproverDto> Approvers { get; set; } = [];
    public List<ApprovalPolicyRuleDto>? Rules { get; set; }
}

public class SetPolicyActiveRequest
{
    public bool IsActive { get; set; }
}

public class UserRoleUpdateRequest
{
    public List<string> Roles { get; set; } = [];
}

public class ApprovalPolicyMetadataDto
{
    public List<string> DocumentTypes { get; set; } = [];
    public Dictionary<string, List<string>> Fields { get; set; } = [];
    public List<string> Operators { get; set; } = [];
}

public class UserWithRolesDto
{
    public int Id { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public List<string> Roles { get; set; } = [];
}
