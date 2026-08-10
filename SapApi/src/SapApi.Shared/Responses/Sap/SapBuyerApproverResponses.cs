namespace SapApi.Shared.Responses.Sap;

public record SapSalesPersonResponse
{
    [JsonPropertyName("SalesEmployeeCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SalesEmployeeCode { get; set; }

    [JsonPropertyName("SalesEmployeeName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SalesEmployeeName { get; set; }

    [JsonPropertyName("Active"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Active { get; set; }
}

public record GetAllSapSalesPersonsResponse : SapBaseResponse
{
    [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SapSalesPersonResponse>? Value { get; set; }
}

public record SapEmployeeInfoResponse
{
    [JsonPropertyName("EmployeeID"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EmployeeID { get; set; }

    [JsonPropertyName("FirstName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FirstName { get; set; }

    [JsonPropertyName("LastName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastName { get; set; }

    [JsonPropertyName("Active"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Active { get; set; }

    [JsonPropertyName("MobilePhone"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MobilePhone { get; set; }

    [JsonPropertyName("OfficePhone"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OfficePhone { get; set; }

    [JsonPropertyName("HomePhone"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HomePhone { get; set; }

    [JsonIgnore]
    public string DisplayName =>
        string.Join(' ', new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

    [JsonIgnore]
    public string? ContactPhone =>
        FirstNonEmpty(MobilePhone, OfficePhone, HomePhone);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}

public record GetAllSapEmployeesInfoResponse : SapBaseResponse
{
    [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SapEmployeeInfoResponse>? Value { get; set; }
}
