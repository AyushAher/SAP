using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Infrastructure.Services;
using SapApi.Shared.Models;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(AuthenticationSchemes = "CustomScheme", Roles = "SuperAdmin")]
public class ActionAuditLogsController(ActionAuditLogService auditLogService) : ControllerBase
{
    [HttpPost("list")]
    public async Task<IActionResult> List([FromBody] PaginationRequest? request, CancellationToken cancellationToken) =>
        Ok(await auditLogService.ListAsync(PaginationRequest.Normalize(request), cancellationToken));
}
