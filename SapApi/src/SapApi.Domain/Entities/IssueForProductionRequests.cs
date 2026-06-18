namespace SapApi.Domain.Entities
{
    public class IssueForProductionRequests
    {
        public int Id { get; set; }
        public string RequestBody { get; set; } = string.Empty;
        public string CardCode { get; set; } = string.Empty;
        public string CardName { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ItemNo { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
    }
}
