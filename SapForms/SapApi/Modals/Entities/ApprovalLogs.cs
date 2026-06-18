namespace SapApi.Modals.Entities
{
    public class ApprovalLogs
    {
        public int Id { get; set; }
        public int ApprovalRequestId { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; } = DateTime.Now;

    }
}
