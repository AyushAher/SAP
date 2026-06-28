namespace Shared.Responses.Sap
{
    public record SapLoginResponse : SapBaseResponse
    {
        [JsonPropertyName("SessionId")] public string? SessionId { get; set; }
        [JsonPropertyName("Version")] public string? Version { get; set; }
        [JsonPropertyName("SessionTimeout")] public int? SessionTimeout { get; set; }
    }
}