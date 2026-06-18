using System.ComponentModel.DataAnnotations;

namespace Shared.Requests
{
    public record SapLoginRequest
    {
        [Required] public string UserName { get; set; } = string.Empty;
        [Required] public string Password { get; set; } = string.Empty;
        [Required] public string CompanyDB { get; set; } = string.Empty;
    }
}