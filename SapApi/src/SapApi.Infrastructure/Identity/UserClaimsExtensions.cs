using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SapApi.Infrastructure.Identity;

public static class UserClaimsExtensions
{
    public static int? GetUserIdAsync(this IHttpContextAccessor httpContextAccessor)
    {
        var id = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(id, out var userId) ? userId : null;
    }
}
