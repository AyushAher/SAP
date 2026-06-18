using System.Text.Json.Serialization;

namespace Shared.Responses
{
    public class BaseAuthServiceResponse
    {
        [JsonPropertyName("succeeded")] public bool Succeeded { get; set; }
        [JsonPropertyName("errorCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ErrorCode { get; set; }
        [JsonPropertyName("errorDescription"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ErrorDescription { get; set; }

    }
}
