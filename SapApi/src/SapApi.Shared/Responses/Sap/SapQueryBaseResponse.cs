namespace SapApi.Shared.Responses.Sap
{
    public record SapQueryBaseResponse
    {
        public string? SqlName { get; set; }
        public string? SqlCode { get; set; }
        public string? ParamList { get; set; }
        public string? SqlText { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}