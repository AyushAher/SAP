using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace SapApi.Modals.Requests.Account
{
    public class RegisterUserRequest
    {
        [NotNull, Required] public string? Email { get; set; }
        [NotNull, Required] public string? UserName { get; set; }
        [NotNull, Required] public string? Password { get; set; }
        [NotNull, Required] public string? ConfirmPassword { get; set; }
        public bool IsActive { get; set; } = true;
    }
}