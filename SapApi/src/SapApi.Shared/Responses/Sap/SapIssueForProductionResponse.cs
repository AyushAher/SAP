using SapApi.Shared.Requests;
namespace SapApi.Shared.Responses.Sap
{
    public record GetAllSapIssueForProductionResponse : SapBaseResponse
    {
        public List<SapIssueForProductionResponse> Value { get; set; } = [];
    }
    public record SapIssueForProductionResponse : SapBaseResponse
    {
        [JsonPropertyName("DocEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

        public int? DocEntry { get; set; }
        [JsonPropertyName("DocNum"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

        public int? DocNum { get; set; }
        [JsonPropertyName("DocType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

        public string? DocType { get; set; }
        [JsonPropertyName("CardCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

        public string? CardCode { get; set; }
        [JsonPropertyName("CardName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

        public string? CardName { get; set; }
        [JsonPropertyName("DocTotal"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

        public double? DocTotal { get; set; }
        [JsonPropertyName("JournalMemo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

        public string? JournalMemo { get; set; }
        [JsonPropertyName("DocDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

        public DateTime? DocDate { get; set; }
        [JsonPropertyName("DueDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

        public DateTime? DueDate { get; set; }
        [JsonPropertyName("Project"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

        public string? Project { get; set; }
        [JsonPropertyName("NumberOfInstallments"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

        public int? NumberOfInstallments { get; set; }
        [JsonPropertyName("DownPayment"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

        public double? DownPayment { get; set; }
        [JsonPropertyName("DocumentLines"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapInventoryGenExitRequestOrderLinesRequest> DocumentLines { get; set; } = [];
    }
}