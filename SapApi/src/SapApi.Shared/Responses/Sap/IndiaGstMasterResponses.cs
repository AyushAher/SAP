using SapApi.Shared.Responses.Sap;

namespace SapApi.Shared.Responses.Sap;

public record IndiaHsnCodeResponse
{
    [JsonPropertyName("AbsEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AbsEntry { get; set; }

    [JsonPropertyName("Chapter"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Chapter { get; set; }

    [JsonPropertyName("Heading"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Heading { get; set; }

    [JsonPropertyName("SubHeading"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SubHeading { get; set; }

    [JsonPropertyName("ChapterID"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChapterID { get; set; }

    [JsonPropertyName("Description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("Dscription"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Dscription { get; set; }

    [JsonIgnore]
    public string DisplayCode =>
        !string.IsNullOrWhiteSpace(ChapterID)
            ? ChapterID
            : string.Join("", new[] { Chapter, Heading, SubHeading }.Where(s => !string.IsNullOrWhiteSpace(s)));

    [JsonIgnore]
    public string DisplayLabel
    {
        get
        {
            var desc = Description ?? Dscription;
            var code = DisplayCode;
            return string.IsNullOrWhiteSpace(desc) ? code : $"{code} - {desc}";
        }
    }
}

public record IndiaHsnListEnvelope
{
    [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<IndiaHsnCodeResponse>? Value { get; set; }
}

public record IndiaSacCodeResponse
{
    [JsonPropertyName("AbsEntry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AbsEntry { get; set; }

    [JsonPropertyName("ServiceCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceCode { get; set; }

    [JsonPropertyName("Description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>SAP names the SAC description ServiceName (not Description) on the IndiaSacCode entity set.</summary>
    [JsonPropertyName("ServiceName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceName { get; set; }

    [JsonIgnore]
    public string DisplayLabel
    {
        get
        {
            var desc = string.IsNullOrWhiteSpace(Description) ? ServiceName : Description;
            var code = ServiceCode ?? AbsEntry?.ToString() ?? "";
            return string.IsNullOrWhiteSpace(desc) ? code : $"{code} - {desc}";
        }
    }
}

public record IndiaSacListEnvelope
{
    [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<IndiaSacCodeResponse>? Value { get; set; }
}
