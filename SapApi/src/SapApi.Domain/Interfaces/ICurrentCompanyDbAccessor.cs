using SapApi.Shared.Enums;

namespace SapApi.Domain.Interfaces;

public interface ICurrentCompanyDbAccessor
{
    SapCompanyDatabase? GetCompanyDb();

    string GetCompanyDbName();
}
