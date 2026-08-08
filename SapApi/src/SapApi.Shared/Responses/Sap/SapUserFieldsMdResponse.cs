namespace SapApi.Shared.Responses.Sap;

public record SapUserFieldsMdValidValue
{
    [JsonPropertyName("Value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; set; }

    [JsonPropertyName("Description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}

public record SapUserFieldsMdResponse
{
    [JsonPropertyName("Name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("TableName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TableName { get; set; }

    [JsonPropertyName("FieldID"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FieldID { get; set; }

    [JsonPropertyName("Size"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Size { get; set; }

    [JsonPropertyName("EditSize"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EditSize { get; set; }

    [JsonPropertyName("ValidValuesMD"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SapUserFieldsMdValidValue>? ValidValuesMD { get; set; }
}

public record GetAllSapUserFieldsMdResponse : SapBaseResponse
{
    [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SapUserFieldsMdResponse>? Value { get; set; }
}
