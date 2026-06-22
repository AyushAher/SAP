using Microsoft.AspNetCore.Http;
using SapApi.Domain.Interfaces;
using SapApi.Shared;

namespace SapApi.Infrastructure.Identity;

public class CurrentBranchAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentBranchAccessor
{
    public int? GetBranchId()
    {
        var value = httpContextAccessor.HttpContext?.User?.FindFirst(SapClaimTypes.Branch)?.Value;
        return int.TryParse(value, out var branchId) ? branchId : null;
    }
}
