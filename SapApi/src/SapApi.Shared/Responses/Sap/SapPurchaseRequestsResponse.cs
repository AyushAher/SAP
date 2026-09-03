using SapApi.Shared.Requests;

namespace SapApi.Shared.Responses.Sap;

/// <summary>
/// SAP Service Layer PurchaseRequests (OPRQ). Shares marketing-document fields with Purchase Orders
/// and adds requester / required-date properties.
/// </summary>
public record SapPurchaseRequestsResponse : SapPurchaseOrdersResponse
{
    /// <summary>SAP user or employee code depending on <see cref="ReqType"/>.</summary>
    [JsonPropertyName("Requester"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Requester { get; set; }

    [JsonPropertyName("RequesterName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequesterName { get; set; }

    [JsonPropertyName("RequesterEmail"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequesterEmail { get; set; }

    [JsonPropertyName("ReqCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReqCode { get; set; }

    /// <summary>12 = SAP user, 171 = employee.</summary>
    [JsonPropertyName("ReqType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ReqType { get; set; }

    [JsonPropertyName("RequesterDepartment"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RequesterDepartment { get; set; }

    [JsonPropertyName("RequesterBranch"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RequesterBranch { get; set; }

    /// <summary>SAP header field is misspelled RequriedDate.</summary>
    [JsonPropertyName("RequriedDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? RequiredDate { get; set; }

    [JsonPropertyName("Cancelled"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cancelled { get; set; }
}

public record GetAllSapPurchaseRequestsResponse : SapBaseResponse
{
    [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SapPurchaseRequestsResponse>? Value { get; set; }
}
