using System.Text.Json.Serialization;
using SapApi.Shared.Responses.Sap;

namespace SapApi.Shared.Requests;

/// <summary>
/// Minimal PATCH body for appending production order lines. Header fields such as PostingDate
/// are omitted so SAP keeps the existing document date. Line identity fields are stripped on
/// the line itself so Service Layer appends rather than updating an existing row.
/// </summary>
public sealed class SapProductionOrderLinePatchRequest
{
    [JsonPropertyName("PostingDate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? PostingDate { get; set; }

    [JsonPropertyName("ProductionOrderLines")]
    public List<SapProductionOrderLines> ProductionOrderLines { get; set; } = [];
}
