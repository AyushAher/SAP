using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RotatingJwt;
using System.Security.Claims;

namespace Tests_Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController(IJwtTokenService jwtTokenService) : ControllerBase
    {
        [HttpPost("AuthToken")]
        public async Task<string> AuthToken(GenerateTokenRequest request)
        {
            var additionalClaims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "10"),
                new(ClaimTypes.Name, "sudarshan.n@jisasoftech.com"),
                new(ClaimTypes.GivenName, "sudarshan.n@jisasoftech.com"),
                new(ClaimTypes.Surname, "Sudarshan Narhe"),
                new(ClaimTypes.Actor, "9846789547"),
                new(ClaimTypes.PrimarySid, "0")
            };

            var tokenResponse = await jwtTokenService.GenerateAccessToken("4", additionalClaims, true);
            return tokenResponse.Token;
        }

        [HttpPost("TokenTest")]
        [Authorize]
        public IActionResult TokenTest()
        {
            return Ok();
        }
    }
}