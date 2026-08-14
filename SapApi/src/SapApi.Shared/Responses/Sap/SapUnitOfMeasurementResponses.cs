namespace SapApi.Shared.Responses.Sap;

public record SapUnitOfMeasurementsResponse : SapBaseResponse
{
    [JsonPropertyName("value")] public List<SapUnitOfMeasurementResponse>? Value { get; set; }
}

/// <summary>UoM master row (OUOM). AbsEntry -1 / Code "Manual" is SAP's pseudo unit, not a real UoM.</summary>
public record SapUnitOfMeasurementResponse
{
    [JsonPropertyName("AbsEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AbsEntry { get; set; }

    [JsonPropertyName("Code"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; set; }

    [JsonPropertyName("Name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }
}

public record SapUnitOfMeasurementGroupsResponse : SapBaseResponse
{
    [JsonPropertyName("value")] public List<SapUnitOfMeasurementGroupResponse>? Value { get; set; }
}

/// <summary>UoM group (OUGP). AbsEntry -1 is the "Manual" group, which has no per-item unit list.</summary>
public record SapUnitOfMeasurementGroupResponse
{
    [JsonPropertyName("AbsEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AbsEntry { get; set; }

    [JsonPropertyName("Code"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; set; }

    [JsonPropertyName("Name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    /// <summary>AbsEntry of the group's base (inventory) unit.</summary>
    [JsonPropertyName("BaseUoM"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BaseUoM { get; set; }

    [JsonPropertyName("UoMGroupDefinitionCollection"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SapUoMGroupDefinitionResponse>? UoMGroupDefinitionCollection { get; set; }
}

/// <summary>
/// One alternate-unit row of a UoM group (UGP1): AlternateQuantity of AlternateUoM equals
/// BaseQuantity of the group's base unit.
/// </summary>
public record SapUoMGroupDefinitionResponse
{
    [JsonPropertyName("AlternateUoM"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AlternateUoM { get; set; }

    [JsonPropertyName("AlternateQuantity"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? AlternateQuantity { get; set; }

    [JsonPropertyName("BaseQuantity"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? BaseQuantity { get; set; }

    [JsonPropertyName("Active"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Active { get; set; }
}
