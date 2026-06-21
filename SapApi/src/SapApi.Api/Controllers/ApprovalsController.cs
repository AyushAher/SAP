using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Identity;
using SapApi.Infrastructure.Persistence;
using SapApi.Infrastructure.Services;
using SapApi.Infrastructure.Sap;
using SapApi.Shared.Models;
using SapApi.Shared.Requests;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/approvals")]
[Authorize]
public class ApprovalsController(
    ApprovalService approvalService,
    ApprovalExecutionService executionService,
    ApprovalRequestViewService requestViewService,
    StageWisePaymentService stageWisePaymentService,
    AppDbContext db,
    IHttpContextAccessor httpContext,
    ICurrentCompanyDbAccessor companyDbAccessor) : ControllerBase
{
    private string CompanyDb => companyDbAccessor.GetCompanyDbName();
    [HttpGet("{requestId:int}")]
    public async Task<IActionResult> GetById(int requestId, CancellationToken cancellationToken)
    {
        var request = await requestViewService.GetRequestAsync(requestId, cancellationToken);
        return request is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Request not found"))
            : Ok(ApiResponse<object>.Ok(request));
    }

    [HttpGet("{requestId:int}/payment-context")]
    public async Task<IActionResult> GetPaymentContext(int requestId, CancellationToken cancellationToken)
    {
        var context = await requestViewService.GetPaymentContextAsync(requestId, cancellationToken);
        return context is null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Payment context not found"))
            : Ok(ApiResponse<object>.Ok(context));
    }

    [HttpPost("pending/list")]
    public async Task<IActionResult> ListPending([FromBody] PaginationRequest? request, CancellationToken cancellationToken)
    {
        var normalized = PaginationRequest.Normalize(request);
        var userId = httpContext.GetUserIdAsync() ?? throw new UnauthorizedAccessException();
        var data = await approvalService.GetPendingForUserAsync(userId);
        var response = InMemoryListPagination.Paginate(data, normalized, static (row, field, value) =>
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            var text = value.ToLowerInvariant();
            return field.ToLowerInvariant() switch
            {
                "documenttype" => row.DocumentType.ToString().ToLowerInvariant().Contains(text),
                "overallstatus" => row.OverallStatus.ToString().ToLowerInvariant().Contains(text),
                "requester" => (row.RequesterUser?.FullName ?? row.RequesterUser?.UserName ?? string.Empty).ToLowerInvariant().Contains(text),
                _ => row.Id.ToString().Contains(text, StringComparison.OrdinalIgnoreCase),
            };
        });

        return Ok(response);
    }

    [HttpPost("my-requests/list")]
    public async Task<IActionResult> ListMyRequests([FromBody] PaginationRequest? request, CancellationToken cancellationToken)
    {
        var normalized = PaginationRequest.Normalize(request);
        var userId = httpContext.GetUserIdAsync() ?? throw new UnauthorizedAccessException();
        var query = db.ApprovalRequests.AsNoTracking()
            .Include(x => x.UserApprovals)
            .Include(x => x.RequesterUser)
            .Include(x => x.Policy)
            .Where(x => x.CompanyDb == CompanyDb && x.RequesterUserId == userId)
            .OrderByDescending(x => x.CreatedAt);

        var (items, totalCount) = await query.ToPaginatedListAsync(normalized, cancellationToken);
        return Ok(PaginationResponseFactory.Create(normalized, items, totalCount));
    }

    [HttpPost("{requestId:int}/approve")]
    public async Task<IActionResult> Approve(int requestId, [FromBody] ApprovalActionData data, CancellationToken cancellationToken)
    {
        var userId = httpContext.GetUserIdAsync() ?? throw new UnauthorizedAccessException();
        var result = await approvalService.ApproveAsync(requestId, userId, data.Comment ?? "Approved");

        if (result?.OverallStatus == ApprovalStatus.Approved)
        {
            var sapResponse = await executionService.ExecuteAsync(result, data, cancellationToken);
            await executionService.FinalizeApprovalAsync(result, data, sapResponse, cancellationToken);
            return Ok(ApiResponse<object>.Ok(new { result, sapResponse }));
        }

        return Ok(ApiResponse<object>.Ok(result, result?.OverallStatus == ApprovalStatus.Pending
            ? "Forwarded for further approval"
            : "Approved"));
    }

    [HttpPost("{requestId:int}/reject")]
    public async Task<IActionResult> Reject(int requestId, [FromBody] ApprovalActionData data, CancellationToken cancellationToken)
    {
        var userId = httpContext.GetUserIdAsync() ?? throw new UnauthorizedAccessException();
        await approvalService.RejectAsync(requestId, userId, data.Comment ?? "Rejected");
        await stageWisePaymentService.MarkRejectedWhenRequestRejectedAsync(requestId);
        return Ok(ApiResponse<object>.Ok(null, "Rejected"));
    }

    [HttpPost("bulk-approve")]
    public async Task<IActionResult> BulkApprove([FromBody] List<int> requestIds, CancellationToken cancellationToken)
    {
        var userId = httpContext.GetUserIdAsync() ?? throw new UnauthorizedAccessException();
        var results = new List<object>();
        foreach (var id in requestIds)
        {
            var result = await approvalService.ApproveAsync(id, userId, "Bulk approved");
            if (result?.OverallStatus == ApprovalStatus.Approved)
            {
                var sapResponse = await executionService.ExecuteAsync(result, new ApprovalActionData { Action = "Approve" }, cancellationToken);
                await executionService.FinalizeApprovalAsync(result, null, sapResponse, cancellationToken);
            }
            results.Add(new { id, result });
        }
        return Ok(ApiResponse<object>.Ok(results));
    }

    [HttpPost("bulk-reject")]
    public async Task<IActionResult> BulkReject([FromBody] BulkRejectRequest request, CancellationToken cancellationToken)
    {
        var userId = httpContext.GetUserIdAsync() ?? throw new UnauthorizedAccessException();
        foreach (var id in request.RequestIds)
        {
            await approvalService.RejectAsync(id, userId, request.Comment ?? "Bulk rejected");
            await stageWisePaymentService.MarkRejectedWhenRequestRejectedAsync(id);
        }
        return Ok(ApiResponse<object>.Ok(null, "Rejected"));
    }
}

public class BulkRejectRequest
{
    public List<int> RequestIds { get; set; } = [];
    public string? Comment { get; set; }
}
