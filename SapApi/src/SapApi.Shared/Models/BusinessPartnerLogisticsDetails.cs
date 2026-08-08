namespace SapApi.Shared.Models;

public record BusinessPartnerLogisticsDetails
{
    public string? CardCode { get; init; }
    public string? CardName { get; init; }
    public string? DefaultShipTo { get; init; }
    public string? DefaultContactPerson { get; init; }
    public List<BusinessPartnerAddressOption> Addresses { get; init; } = [];
    public List<BusinessPartnerContactOption> Contacts { get; init; } = [];
}

public record BusinessPartnerAddressOption
{
    public string AddressName { get; init; } = string.Empty;
    public string AddressType { get; init; } = string.Empty;
    public string FormattedAddress { get; init; } = string.Empty;
}

public record BusinessPartnerContactOption
{
    public int? InternalCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Position { get; init; }
    public string? Phone { get; init; }
}
