namespace SapApi.Shared.Responses.Sap
{
    public record SapBranchesResponse : SapBaseResponse
    {
        [JsonPropertyName("BPLID")]
        public int BplId { get; set; }
        
        [JsonPropertyName("BPLName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BplName { get; set; }

        [JsonPropertyName("FederalTaxID"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FederalTaxID { get; set; }

        [JsonPropertyName("Address"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Address { get; set; }
        
        [JsonPropertyName("U_PANNO"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PanNo { get; set; }
    }

    public record SapGetAllBranchesResponse : SapBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapBranchesResponse>? Value { get; set; }

    }
}