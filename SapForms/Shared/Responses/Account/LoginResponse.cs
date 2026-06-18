using Shared;
using Shared.Responses;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Shared.Responses.Account
{
    public class LoginResponse : BaseAuthServiceResponse
    {
        [JsonPropertyName("incorrectCredentials")]
        public bool IncorrectCredentials { get; set; }

        [JsonPropertyName("isLocked")] public bool IsLocked { get; set; }

        [JsonPropertyName("isTwoFactorRequired")]
        public bool IsTwoFactorRequired { get; set; }

        [JsonPropertyName("emailNotConfirmed")]
        public bool EmailNotConfirmed { get; set; }

        [JsonPropertyName("phoneNotConfirmed")]
        public bool PhoneNotConfirmed { get; set; }

        [JsonPropertyName("isNotActive")] public bool IsNotActive { get; set; }

        [JsonPropertyName("claims"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ClaimsDto> Claims { get; set; } = [];

        [JsonPropertyName("Token"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Token { get; set; }

        [JsonPropertyName("refreshToken"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RefreshToken { get; set; }
    }
}