namespace SapApi.Modals.Responses.Sap
{
    public record SapBaseResponse
    {
        [JsonPropertyName("odata.metadata"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ODataMetadata { get; set; }

        [JsonPropertyName("error"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SapError? Error { get; set; }
    }
}