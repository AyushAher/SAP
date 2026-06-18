namespace SapApi.Shared.Models;

public class MasterLookupRequest
{
    public List<string> ItemCodes { get; set; } = [];
    public List<string> ProjectCodes { get; set; } = [];
    public List<string> CardCodes { get; set; } = [];
}

public class MasterLookupResponse
{
    public Dictionary<string, string> Items { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Projects { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> BusinessPartners { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
