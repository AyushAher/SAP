namespace SapApi.Shared.Responses.Sap;

public record SapChartOfAccountResponse
{
    [JsonPropertyName("Code"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; set; }

    [JsonPropertyName("Name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("ActiveAccount"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ActiveAccount { get; set; }
}

public record GetAllSapChartOfAccountsResponse : SapBaseResponse
{
    [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SapChartOfAccountResponse>? Value { get; set; }
}
