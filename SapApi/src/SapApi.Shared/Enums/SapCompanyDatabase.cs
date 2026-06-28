namespace SapApi.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SapCompanyDatabase
{
    PBBPL_LIVE,
    PBBPL_UAT
}
