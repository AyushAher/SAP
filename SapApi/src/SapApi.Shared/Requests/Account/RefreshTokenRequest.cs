using SapApi.Shared.Enums;

namespace SapApi.Shared.Requests.Account;

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
    public SapCompanyDatabase? CompanyDb { get; set; }
}
