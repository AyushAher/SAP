using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;

namespace SapApi.Infrastructure.Services;

public class ApprovalPolicyService(AppDbContext context, ICurrentCompanyDbAccessor companyDbAccessor)
{
    private string CompanyDb => companyDbAccessor.GetCompanyDbName();

    #region CREATE

    public async Task<int> CreatePolicyAsync(
        ApprovalDocumentType docType,
        int requesterId,
        List<ApprovalPolicyApprover> approvers,
        List<ApprovalPolicyRule>? rules = null)
    {
        ValidateApprovers(approvers);
        await ValidateNoActiveDuplicateAsync(docType, requesterId, excludePolicyId: null);

        var policy = new ApprovalPolicy
        {
            CompanyDb = CompanyDb,
            DocumentType = docType,
            RequesterUserId = requesterId,
            IsActive = true,
            Approvers = approvers,
            Rules = rules ?? []
        };

        context.ApprovalPolicies.Add(policy);
        await context.SaveChangesAsync();

        return policy.Id;
    }

    #endregion

    #region UPDATE

    public async Task UpdatePolicyAsync(
        int policyId,
        ApprovalDocumentType docType,
        int requesterId,
        List<ApprovalPolicyApprover> approvers,
        List<ApprovalPolicyRule>? rules = null)
    {
        ValidateApprovers(approvers);
        await ValidateNoActiveDuplicateAsync(docType, requesterId, excludePolicyId: policyId);

        ApprovalPolicy policy = await context.ApprovalPolicies
            .Include(p => p.Approvers)
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == policyId && p.CompanyDb == CompanyDb)
            ?? throw new ApiErrorException(BaseErrorCodes.NullValue, "Policy not found.");

        policy.DocumentType = docType;
        policy.RequesterUserId = requesterId;

        context.ApprovalPolicyApprovers.RemoveRange(policy.Approvers);
        policy.Approvers = approvers;

        context.ApprovalPolicyRules.RemoveRange(policy.Rules);
        policy.Rules = rules ?? [];

        await context.SaveChangesAsync();
    }

    #endregion

    #region GET

    public async Task<List<ApprovalPolicy>> GetAllAsync()
    {
        return await context.ApprovalPolicies
            .Where(p => p.CompanyDb == CompanyDb)
            .Include(p => p.RequesterUser)
            .Include(p => p.Approvers)
            .Include(p => p.Rules)
            .ToListAsync();
    }

    public async Task<ApprovalPolicy?> GetByIdAsync(int id)
    {
        return await context.ApprovalPolicies
            .Where(p => p.CompanyDb == CompanyDb)
            .Include(p => p.RequesterUser)
            .Include(p => p.Approvers)
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    #endregion

    #region ACTIVATE / DEACTIVATE

    /// <summary>
    /// Lets admins pause/resume a policy without deleting its approver/rule configuration.
    /// </summary>
    public async Task SetActiveAsync(int id, bool isActive)
    {
        ApprovalPolicy policy = await context.ApprovalPolicies
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyDb == CompanyDb)
            ?? throw new ApiErrorException(BaseErrorCodes.NullValue, "Policy not found.");

        if (isActive && !policy.IsActive)
            await ValidateNoActiveDuplicateAsync(policy.DocumentType, policy.RequesterUserId, excludePolicyId: id);

        policy.IsActive = isActive;
        await context.SaveChangesAsync();
    }

    #endregion

    #region DELETE

    public async Task DeletePolicyAsync(int id)
    {
        ApprovalPolicy? policy = await context.ApprovalPolicies
            .Include(p => p.Approvers)
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyDb == CompanyDb);

        if (policy == null)
            return;

        context.ApprovalPolicyApprovers.RemoveRange(policy.Approvers);
        context.ApprovalPolicyRules.RemoveRange(policy.Rules);
        context.ApprovalPolicies.Remove(policy);

        await context.SaveChangesAsync();
    }

    #endregion

    #region VALIDATION

    private static void ValidateApprovers(List<ApprovalPolicyApprover> approvers)
    {
        if (approvers == null || !approvers.Any())
            throw new ApiErrorException(BaseErrorCodes.ValidationFailed, "At least one approver required.");

        if (approvers.Any(x => x.Priority < 1))
            throw new ApiErrorException(BaseErrorCodes.ValidationFailed, "Priority must start from 1.");

        if (!approvers.Any(x => x.Priority == 1))
            throw new ApiErrorException(BaseErrorCodes.ValidationFailed, "At least one Priority 1 approver required.");

        var duplicateUsers = approvers
            .GroupBy(x => x.ApproverUserId)
            .Any(g => g.Count() > 1);

        if (duplicateUsers)
            throw new ApiErrorException(BaseErrorCodes.ValidationFailed, "Duplicate approvers not allowed.");
    }

    /// <summary>
    /// Only one active policy may exist per (requester, document type). CheckApprovalPolicy resolves the
    /// applicable policy with FirstOrDefaultAsync, so a second active match here would silently make
    /// approval routing non-deterministic instead of raising a visible error.
    /// </summary>
    private async Task ValidateNoActiveDuplicateAsync(ApprovalDocumentType docType, int requesterId, int? excludePolicyId)
    {
        var duplicateExists = await context.ApprovalPolicies.AnyAsync(p =>
            p.CompanyDb == CompanyDb &&
            p.DocumentType == docType &&
            p.RequesterUserId == requesterId &&
            p.IsActive &&
            p.Id != excludePolicyId);

        if (duplicateExists)
            throw new ApiErrorException(
                BaseErrorCodes.Conflict,
                "An active approval policy already exists for this requester and document type.");
    }

    #endregion
}
