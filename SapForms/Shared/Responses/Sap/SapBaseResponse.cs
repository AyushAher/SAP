namespace Shared.Responses.Sap
{
    public record SapBaseResponse
    {
        [JsonPropertyName("odata.metadata"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ODataMetadata { get; set; }

        [JsonPropertyName("odata.nextLink"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ODataNextLink { get; set; }

        [JsonPropertyName("error"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SapError? Error { get; set; }
        [JsonIgnore] public int? BaseDocEntry { get; set; }
        [JsonIgnore] public int? BaseDocNum { get; set; }
        [JsonIgnore] public string? ApprovalDocEntry { get; set; }
        [JsonIgnore] public string? ApprovalDocNumber { get; set; }
        [JsonIgnore] public bool PendingApproval { get; set; } = false;
        [JsonIgnore] public string? SupportingData { get; set; }
        [JsonIgnore] public int? PendingApprovalRequestId { get; set; }
        [JsonIgnore] public bool HasNextPage => !string.IsNullOrEmpty(ODataNextLink);
    }
}