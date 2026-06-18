using Microsoft.AspNetCore.Identity;
using SapApi.Domain.Entities;
using SapApi.Shared;

namespace SapApi.Infrastructure.Persistence;

public static class IdentityDataSeeder
{
    public static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var roleName in new[] { Constants.Roles.SuperAdmin, Constants.Roles.Admin, Constants.Roles.Standard })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
        }
    }
}
