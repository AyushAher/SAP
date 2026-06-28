using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Shared.Models;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/users")]
// [Authorize(Roles = "Admin,SuperAdmin")]
public class UsersController(UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpPost("list")]
    public async Task<IActionResult> List([FromBody] PaginationRequest? request, CancellationToken cancellationToken)
    {
        var normalized = PaginationRequest.Normalize(request);
        var pageSize = normalized.PageSize ?? 20;
        var query = userManager.Users.AsNoTracking();

        foreach (var filter in normalized.Filters)
        {
            if (filter.Operator == "contains" && filter.Field.Equals("email", StringComparison.OrdinalIgnoreCase))
            {
                var emailFilter = filter.Value?.ToString() ?? string.Empty;
                query = query.Where(u => u.Email != null && u.Email.Contains(emailFilter));
            }
            if (filter.Operator == "eq" && filter.Field.Equals("userName", StringComparison.OrdinalIgnoreCase))
            {
                var userNameFilter = filter.Value?.ToString() ?? string.Empty;
                query = query.Where(u => u.UserName == userNameFilter);
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(u => u.Id)
            .Skip((normalized.PageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new { u.Id, u.UserName, u.Email, u.FullName })
            .ToListAsync(cancellationToken);

        return Ok(new PaginationResponse<object>
        {
            Success = true,
            Data = users,
            PageNumber = normalized.PageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            Filters = normalized.Filters,
            Sorts = normalized.Sorts
        });
    }
}
