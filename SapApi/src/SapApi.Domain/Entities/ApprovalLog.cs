namespace SapApi.Domain.Entities
{
    public class ApprovalLog
    {
        public int Id { get; set; }
        public string CompanyDb { get; set; } = string.Empty;
        public int ApprovalRequestId { get; set; }

        public int ActionByUserId { get; set; }
        public string Action { get; set; } = string.Empty;

        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApprovalRequest? ApprovalRequest { get; set; }
    }
}