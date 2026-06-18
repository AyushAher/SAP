namespace SapApi.Domain.Entities
{
    public class ApprovalLog
    {
        public int Id { get; set; }
        public int ApprovalRequestId { get; set; }

        public int ActionByUserId { get; set; }
        public string Action { get; set; }

        public string? OldValue { get; set; }
        public string? NewValue { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}