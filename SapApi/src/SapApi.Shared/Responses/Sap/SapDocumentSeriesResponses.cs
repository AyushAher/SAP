namespace SapApi.Shared.Responses.Sap;

/// <summary>One row from SeriesService_GetDocumentSeries (SAPB1.Series).</summary>
public sealed class SapDocumentSeriesEntry
{
    [JsonPropertyName("Series")]
    public int Series { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("Document")]
    public string? Document { get; set; }

    [JsonPropertyName("PeriodIndicator")]
    public string? PeriodIndicator { get; set; }

    [JsonPropertyName("BPLID")]
    public int? BPLID { get; set; }

    [JsonPropertyName("Locked")]
    public string? Locked { get; set; }

    [JsonPropertyName("IsManual")]
    public string? IsManual { get; set; }
}

public sealed class SapDocumentSeriesListResponse
{
    [JsonPropertyName("value")]
    public List<SapDocumentSeriesEntry>? Value { get; set; }
}
