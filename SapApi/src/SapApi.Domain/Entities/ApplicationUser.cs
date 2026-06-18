using Microsoft.AspNetCore.Identity;

namespace SapApi.Domain.Entities
{
    public class ApplicationUser : IdentityUser<int>
    {
        public string? FullName { get; set; }
        public ICollection<ApprovalRequest> ApprovalRequest { get; set; } = [];
        public ICollection<ApprovalPolicy> Policy { get; set; } = [];
        public ICollection<ApprovalPolicyApprover> PolicyApprover { get; set; } = [];

    }

    public class ApplicationRole : IdentityRole<int>
    {
    }

}
