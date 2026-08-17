namespace SapApi.Shared.Responses.Sap
{
    public class SapBusinessPartnerResponse : SapError
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<SapBusinessPartner>? Value { get; set; }
    }

    public record WithholdingTaxDataCollectionResponse : SapBaseResponse
    {
        [JsonPropertyName("WTName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WtName { get; set; }
        [JsonPropertyName("WTCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? WtCode { get; set; }
        [JsonPropertyName("Rate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public double? Rate { get; set; }
    }

    public record GetAllWithholdingTaxDataCollectionResponse : SapBaseResponse
    {
        [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<WithholdingTaxDataCollectionResponse>? Value { get; set; }
    }

    public record SapBusinessPartner
    {
        [JsonPropertyName("CardCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CardCode { get; set; }
        [JsonPropertyName("CardName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CardName { get; set; }
        [JsonPropertyName("CardForeignName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CardForeignName { get; set; }
        [JsonPropertyName("CardType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CardType { get; set; }
        [JsonPropertyName("GroupCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? GroupCode { get; set; }
        [JsonPropertyName("Series"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Series { get; set; }
        [JsonPropertyName("ShipToDefault"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ShipToDefault { get; set; }
        [JsonPropertyName("BilltoDefault"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? BillToDefault { get; set; }
        [JsonPropertyName("ContactPerson"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ContactPerson { get; set; }
        [JsonPropertyName("EmailAddress"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? EmailAddress { get; set; }
        [JsonPropertyName("BPWithholdingTaxCollection"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<SapWithholdingTaxDataCollectionResponse>? WithholdingTaxDataCollectionResponse { get; set; }
        [JsonPropertyName("BPAddresses"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<SapBusinessPartnerAddress>? BPAddresses { get; set; }
        [JsonPropertyName("ContactEmployees"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<SapBusinessPartnerContact>? ContactEmployees { get; set; }
    }

    public record SapBusinessPartnerAddress
    {
        [JsonPropertyName("AddressName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AddressName { get; set; }
        [JsonPropertyName("AddressType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AddressType { get; set; }
        [JsonPropertyName("Street"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Street { get; set; }
        [JsonPropertyName("StreetNo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? StreetNo { get; set; }
        [JsonPropertyName("Block"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Block { get; set; }
        [JsonPropertyName("ZipCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ZipCode { get; set; }
        [JsonPropertyName("City"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? City { get; set; }
        [JsonPropertyName("County"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? County { get; set; }
        [JsonPropertyName("Country"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Country { get; set; }
        [JsonPropertyName("State"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? State { get; set; }
        [JsonPropertyName("BuildingFloorRoom"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? BuildingFloorRoom { get; set; }
        [JsonPropertyName("AddressName2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AddressName2 { get; set; }
        [JsonPropertyName("AddressName3"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AddressName3 { get; set; }
        /// <summary>India localisation: GSTIN lives on the address, not on the BP header.</summary>
        [JsonPropertyName("GSTIN"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Gstin { get; set; }
    }

    public record SapBusinessPartnerContact
    {
        [JsonPropertyName("InternalCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? InternalCode { get; set; }
        [JsonPropertyName("Name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; set; }
        [JsonPropertyName("FirstName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? FirstName { get; set; }
        [JsonPropertyName("LastName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? LastName { get; set; }
        [JsonPropertyName("Position"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Position { get; set; }
        [JsonPropertyName("Phone1"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Phone1 { get; set; }
        [JsonPropertyName("MobilePhone"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MobilePhone { get; set; }
        [JsonPropertyName("Active"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Active { get; set; }
    }
}
