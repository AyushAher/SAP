namespace SapApi.Domain.Entities
{
    public class ApprovalPolicyApprover
    {
        public int Id { get; set; }

        public int ApprovalPolicyId { get; set; }
        public ApprovalPolicy Policy { get; set; }

        public int ApproverUserId { get; set; }
        public ApplicationUser ApproverUser { get; set; }

        public int Priority { get; set; }
    }
}