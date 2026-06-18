using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Shared.Entities;
using System.Security.Claims;

namespace SapForm.Services
{
    public class CustomClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        IOptions<IdentityOptions> optionsAccessor, RoleManager<ApplicationRole> roleManager)
                : UserClaimsPrincipalFactory<ApplicationUser>(userManager, optionsAccessor)
    {
        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            // Remove existing Name claim (optional but cleaner)
            var existingNameClaim = identity.FindFirst(ClaimTypes.Name);
            if (existingNameClaim != null)
            {
                identity.RemoveClaim(existingNameClaim);
            }

            var roles = await userManager.GetRolesAsync(user);
            identity.AddClaim(new Claim(ClaimTypes.Role, string.Join(",", roles)));
            // Set username as Name
            identity.AddClaim(new Claim(ClaimTypes.Name, user.FullName ?? ""));
            return identity;
        }
    }
}