using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using SapApi.Shared.Enums;

namespace SapApi.Shared.Requests.Account
{
    public class RegisterUserRequest
    {
        [NotNull, Required] public string? FullName { get; set; }
        [NotNull, Required, EmailAddress] public string? Email { get; set; }
        [NotNull, Required] public string? UserName { get; set; }
        [NotNull, Required] public string? Password { get; set; }
        [Required] public SapCompanyDatabase CompanyDb { get; set; }
        public bool IsActive { get; set; } = true;
    }
}