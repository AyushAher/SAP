using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapApi.Domain.Entities;
using SapApi.Infrastructure.Services;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Models;

namespace SapApi.Api.Controllers;

[ApiController]
[Route("api/approval-policies")]
[Authorize]
public class ApprovalPoliciesController(ApprovalPolicyService policyService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetAll() =>
        Ok(ApiResponse<object>.Ok((await policyService.GetAllAsync()).Select(Map)));

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetById(int id)
    {
        var policy = await policyService.GetByIdAsync(id);
        return policy == null
            ? NotFound(ApiResponse<object>.Fail("SYS-02", "Policy not found"))
            : Ok(ApiResponse<object>.Ok(Map(policy)));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Create([FromBody] UpsertApprovalPolicyRequest request)
    {
        var id = await policyService.CreatePolicyAsync(
            request.DocumentType, request.RequesterUserId,
            MapApprovers(request.Approvers), MapRules(request.Rules));
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertApprovalPolicyRequest request)
    {
        await policyService.UpdatePolicyAsync(
            id, request.DocumentType, request.RequesterUserId,
            MapApprovers(request.Approvers), MapRules(request.Rules));
        return Ok(ApiResponse<object>.Ok(null, "Updated"));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        await policyService.DeletePolicyAsync(id);
        return Ok(ApiResponse<object>.Ok(null, "Deleted"));
    }

    [HttpGet("metadata")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public IActionResult GetMetadata() =>
        Ok(ApiResponse<object>.Ok(new ApprovalPolicyMetadataDto
        {
            DocumentTypes = Enum.GetNames(typeof(ApprovalDocumentType))
                .Where(name => !string.Equals(name, nameof(ApprovalDocumentType.None), StringComparison.Ordinal))
                .ToList(),
            Fields = Constants.ApprovalDocFields.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value),
            Operators = Constants.ApprovalOperator.ToList(),
        }));

    private static ApprovalPolicyDto Map(ApprovalPolicy p) => new()
    {
        Id = p.Id,
        DocumentType = p.DocumentType,
        RequesterUserId = p.RequesterUserId,
        RequesterName = p.RequesterUser?.FullName ?? p.RequesterUser?.UserName,
        IsActive = p.IsActive,
        Approvers = p.Approvers.Select(a => new ApprovalPolicyApproverDto
        {
            ApproverUserId = a.ApproverUserId,
            Priority = a.Priority
        }).ToList(),
        Rules = p.Rules.Select(r => new ApprovalPolicyRuleDto
        {
            FieldName = r.FieldName,
            Operator = r.Operator,
            Value = r.Value
        }).ToList()
    };

    private static List<ApprovalPolicyApprover> MapApprovers(List<ApprovalPolicyApproverDto> dtos) =>
        dtos.Select(a => new ApprovalPolicyApprover
        {
            ApproverUserId = a.ApproverUserId,
            Priority = a.Priority
        }).ToList();

    private static List<ApprovalPolicyRule>? MapRules(List<ApprovalPolicyRuleDto>? dtos) =>
        dtos?.Select(r => new ApprovalPolicyRule
        {
            FieldName = r.FieldName,
            Operator = r.Operator,
            Value = r.Value
        }).ToList();
}
