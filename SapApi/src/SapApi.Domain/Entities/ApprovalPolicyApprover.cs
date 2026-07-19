using System.Text.Json.Serialization;

namespace SapApi.Domain.Entities
{
    public class ApprovalPolicyApprover
    {
        public int Id { get; set; }

        public int ApprovalPolicyId { get; set; }

        // Back-reference to the parent — redundant since this entity is always accessed via
        // ApprovalPolicy.Approvers, and EF's fixup would otherwise create a direct cycle.
        [JsonIgnore] public ApprovalPolicy Policy { get; set; }

        public int ApproverUserId { get; set; }
        public ApplicationUser ApproverUser { get; set; }

        public int Priority { get; set; }
    }
}