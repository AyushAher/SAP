using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Domain.Entities;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Models;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/user-groups")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class UserGroupsController(UserGroupService userGroupService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(ApiResponse<object>.Ok((await userGroupService.GetAllAsync()).Select(Map)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var group = await userGroupService.GetByIdAsync(id);
        return group == null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "User group not found"))
            : Ok(ApiResponse<object>.Ok(Map(group)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertUserGroupRequest request)
    {
        var id = await userGroupService.CreateAsync(request.Name, request.Description, request.MemberUserIds);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertUserGroupRequest request)
    {
        await userGroupService.UpdateAsync(id, request.Name, request.Description, request.MemberUserIds);
        return Ok(ApiResponse<object>.Ok(null, "Updated"));
    }

    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetUserGroupActiveRequest request)
    {
        await userGroupService.SetActiveAsync(id, request.IsActive);
        return Ok(ApiResponse<object>.Ok(null, request.IsActive ? "Activated" : "Deactivated"));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await userGroupService.DeleteAsync(id);
        return Ok(ApiResponse<object>.Ok(null, "Deleted"));
    }

    private static UserGroupDto Map(UserGroup g) => new()
    {
        Id = g.Id,
        Name = g.Name,
        Description = g.Description,
        IsActive = g.IsActive,
        CreatedAt = g.CreatedAt,
        Members = g.Members.Select(m => new UserGroupMemberDto
        {
            UserId = m.UserId,
            UserName = m.User?.UserName,
            FullName = m.User?.FullName,
            Email = m.User?.Email,
        }).ToList(),
    };
}
