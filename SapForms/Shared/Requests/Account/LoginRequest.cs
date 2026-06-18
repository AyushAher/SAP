using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Shared.Requests.Account
{
    public class LoginRequest
    {
        [JsonPropertyName("userName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [ Required, NotNull] public string? UserName { get; set; }
        [JsonPropertyName("password"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [Required, NotNull] public string? Password { get; set; }
    }
}
