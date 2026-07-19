using Microsoft.AspNetCore.Identity;
using System.Text.Json.Serialization;

namespace SapApi.Domain.Entities
{
    public class ApplicationUser : IdentityUser<int>
    {
        public string? FullName { get; set; }

        // Inverse navigations only — EF Core wires these up automatically during query materialization
        // (even with AsNoTracking) whenever a user appears more than once in the same Include graph.
        // They must never be serialized from this side or they create reference cycles back through
        // the owning entity (ApprovalRequest/ApprovalPolicy/ApprovalPolicyApprover -> RequesterUser -> here -> ...).
        [JsonIgnore] public ICollection<ApprovalRequest> ApprovalRequest { get; set; } = [];
        [JsonIgnore] public ICollection<ApprovalPolicy> Policy { get; set; } = [];
        [JsonIgnore] public ICollection<ApprovalPolicyApprover> PolicyApprover { get; set; } = [];

    }

    public class ApplicationRole : IdentityRole<int>
    {
    }

}
