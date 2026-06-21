using Microsoft.AspNetCore.Http;
using SapApi.Domain.Interfaces;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;

namespace SapApi.Infrastructure.Identity;

public class CurrentCompanyDbAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentCompanyDbAccessor
{
    public SapCompanyDatabase? GetCompanyDb()
    {
        var value = httpContextAccessor.HttpContext?.User?.FindFirst(SapClaimTypes.CompanyDb)?.Value;
        return Enum.TryParse<SapCompanyDatabase>(value, ignoreCase: false, out var companyDb)
            ? companyDb
            : null;
    }

    public string GetCompanyDbName() =>
        GetCompanyDb()?.ToString()
        ?? throw new ApiErrorException(BaseErrorCodes.NullValue, "Company database context is not available.");
}
