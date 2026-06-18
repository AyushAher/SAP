using System.Text.Json.Serialization;

namespace SapApi.Modals.Responses.Sap
{
    public class SapError
    {
        [JsonPropertyName("code")] public int? Code { get; set; }
        [JsonPropertyName("message")] public SapMessage? Message { get; set; }
    }
    public class SapMessage
    {
        [JsonPropertyName("lang")] public string? Lang { get; set; }
        [JsonPropertyName("value")] public string? Value { get; set; }

    }
}