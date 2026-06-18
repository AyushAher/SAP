using Microsoft.AspNetCore.Identity;

namespace Shared.Entities
{
    public class ApplicationUser : IdentityUser<int>
    {
        public string? FullName { get; set; }
        public ICollection<ApprovalRequest> ApprovalRequest { get; set; } = new List<ApprovalRequest>();
        public ICollection<ApprovalPolicy> Policy { get; set; } = new List<ApprovalPolicy>();
        public ICollection<ApprovalPolicyApprover> PolicyApprover { get; set; } = new List<ApprovalPolicyApprover>();

    }

    public class ApplicationRole : IdentityRole<int>
    {
    }

}
