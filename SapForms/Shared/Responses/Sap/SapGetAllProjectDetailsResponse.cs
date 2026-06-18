
namespace Shared.Responses.Sap
{
    public record SapProjectDetailsResponse : SapBaseResponse
    {
        [JsonPropertyName("Name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ProjectName { get; set; }
        [JsonPropertyName("Code"),JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ProjectCode { get; set; }
    }

    public record SapGetAllProjectDetailsResponse : SapBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SapProjectDetailsResponse>? Value { get; set; }
    }
}
