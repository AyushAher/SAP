using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using SapApi.Shared.Enums;

namespace SapApi.Shared.Requests.Account;

public class SwitchCompanyRequest
{
    [Required, NotNull]
    public SapCompanyDatabase CompanyDb { get; set; }

    [JsonPropertyName("password"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Required, NotNull]
    public string? Password { get; set; }
}
