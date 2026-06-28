namespace Shared.Entities
{
    public class UserApproval
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ApprovalRequestId { get; set; }
        public int Priority { get; set; }
        public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
        public string? Comment { get; set; }
        public DateTime? ActionDate { get; set; }
        public ApprovalRequest ApprovalRequest { get; set; }
        public ApplicationUser User { get; set; }
    }
}