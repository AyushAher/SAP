using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Shared.Models;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/user-roles")]
[Authorize]
public class UserRolesController(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> GetUsersWithRoles(CancellationToken cancellationToken)
    {
        var users = await userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
        var result = new List<UserWithRolesDto>();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new UserWithRolesDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                FullName = user.FullName,
                Roles = roles.ToList(),
            });
        }
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("roles")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await roleManager.Roles.AsNoTracking().Select(r => r.Name).ToListAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(roles));
    }

    [HttpPut("users/{userId:int}/roles")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> UpdateUserRoles(int userId, [FromBody] UserRoleUpdateRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null) return NotFound(ApiResponse<object>.Fail("SYS-02", "User not found"));

        var currentRoles = await userManager.GetRolesAsync(user);
        var toRemove = currentRoles.Except(request.Roles, StringComparer.OrdinalIgnoreCase).ToList();
        var toAdd = request.Roles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();

        if (toRemove.Count > 0) await userManager.RemoveFromRolesAsync(user, toRemove);
        if (toAdd.Count > 0) await userManager.AddToRolesAsync(user, toAdd);

        return Ok(ApiResponse<object>.Ok(await userManager.GetRolesAsync(user)));
    }
}
