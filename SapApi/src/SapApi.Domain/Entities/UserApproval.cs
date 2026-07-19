using System.Text.Json.Serialization;

namespace SapApi.Domain.Entities
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

        // Back-reference to the parent — redundant to serialize since this entity is always accessed
        // via ApprovalRequest.UserApprovals, and EF's fixup would otherwise create a direct cycle.
        [JsonIgnore] public ApprovalRequest ApprovalRequest { get; set; }
        public ApplicationUser User { get; set; }
    }
}