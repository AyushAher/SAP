using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SapApi.Shared;
using SapApi.Shared.Enums;

namespace SapApi.Infrastructure.Identity;

public static class UserClaimsExtensions
{
    public static int? GetUserIdAsync(this IHttpContextAccessor httpContextAccessor)
    {
        var id = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(id, out var userId) ? userId : null;
    }

    public static SapCompanyDatabase? GetCompanyDb(this IHttpContextAccessor httpContextAccessor)
    {
        var value = httpContextAccessor.HttpContext?.User?.FindFirst(SapClaimTypes.CompanyDb)?.Value;
        return Enum.TryParse<SapCompanyDatabase>(value, ignoreCase: false, out var companyDb)
            ? companyDb
            : null;
    }
}
