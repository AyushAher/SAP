namespace SapApi.Modals.Entities
{
    public class ApprovalRequests
    {
        public int Id { get; set; }
        public string? ApprovalModule { get; set; }
        public string? ApproverRoleRequired { get; set; }
        public string? RequestBody { get; set; }
        public Guid? ApprovedByUser { get; set; }
        public bool Approved { get; set; }
        public DateTime? ApprovalDateTime { get; set; }
    }
}