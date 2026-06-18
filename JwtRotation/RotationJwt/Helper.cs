
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;

namespace RotatingJwt
{
    internal static class Helper
    {
        public static List<Claim> GetClaimsFromJwt(this string jwtToken)
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwtToken);

            return token.Claims.ToList();
        }

    }
}
