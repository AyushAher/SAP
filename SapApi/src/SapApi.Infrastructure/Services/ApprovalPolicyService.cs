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
        ApprovalRequesterType requesterType,
        int? requesterUserId,
        int? requesterGroupId,
        List<ApprovalPolicyApprover> approvers,
        List<ApprovalPolicyRule>? rules = null)
    {
        ValidateApprovers(approvers);
        var (userId, groupId) = await ResolveRequesterAsync(requesterType, requesterUserId, requesterGroupId);
        await ValidateNoActiveDuplicateAsync(docType, requesterType, userId, groupId, excludePolicyId: null);

        var policy = new ApprovalPolicy
        {
            CompanyDb = CompanyDb,
            DocumentType = docType,
            RequesterType = requesterType,
            RequesterUserId = userId,
            RequesterGroupId = groupId,
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
        ApprovalRequesterType requesterType,
        int? requesterUserId,
        int? requesterGroupId,
        List<ApprovalPolicyApprover> approvers,
        List<ApprovalPolicyRule>? rules = null)
    {
        ValidateApprovers(approvers);
        var (userId, groupId) = await ResolveRequesterAsync(requesterType, requesterUserId, requesterGroupId);
        await ValidateNoActiveDuplicateAsync(docType, requesterType, userId, groupId, excludePolicyId: policyId);

        ApprovalPolicy policy = await context.ApprovalPolicies
            .Include(p => p.Approvers)
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == policyId && p.CompanyDb == CompanyDb)
            ?? throw new ApiErrorException(BaseErrorCodes.NullValue, "Policy not found.");

        policy.DocumentType = docType;
        policy.RequesterType = requesterType;
        policy.RequesterUserId = userId;
        policy.RequesterGroupId = groupId;

        context.ApprovalPolicyApprovers.RemoveRange(policy.Approvers);
        policy.Approvers = approvers;

        context.ApprovalPolicyRules.RemoveRange(policy.Rules);
        policy.Rules = rules ?? [];

        // Update() walks the whole graph and attaches/marks it (Modified for the policy's own
        // scalar changes, Added for the newly-assigned Approvers/Rules) — required because queries
        // are untracked by default (QueryTrackingBehavior.NoTracking).
        context.ApprovalPolicies.Update(policy);
        await context.SaveChangesAsync();
    }

    #endregion

    #region GET

    public async Task<List<ApprovalPolicy>> GetAllAsync()
    {
        return await context.ApprovalPolicies
            .Where(p => p.CompanyDb == CompanyDb)
            .Include(p => p.RequesterUser)
            .Include(p => p.RequesterGroup)
            .Include(p => p.Approvers)
            .Include(p => p.Rules)
            .ToListAsync();
    }

    public async Task<ApprovalPolicy?> GetByIdAsync(int id)
    {
        return await context.ApprovalPolicies
            .Where(p => p.CompanyDb == CompanyDb)
            .Include(p => p.RequesterUser)
            .Include(p => p.RequesterGroup)
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
        {
            await ValidateNoActiveDuplicateAsync(
                policy.DocumentType,
                policy.RequesterType,
                policy.RequesterUserId,
                policy.RequesterGroupId,
                excludePolicyId: id);
        }

        policy.IsActive = isActive;
        context.ApprovalPolicies.Update(policy);
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

    private async Task<(int? UserId, int? GroupId)> ResolveRequesterAsync(
        ApprovalRequesterType requesterType,
        int? requesterUserId,
        int? requesterGroupId)
    {
        if (requesterType == ApprovalRequesterType.User)
        {
            if (!requesterUserId.HasValue || requesterUserId.Value <= 0)
                throw new ApiErrorException(BaseErrorCodes.ValidationFailed, "Requester user is required.");

            var userExists = await context.Users.AnyAsync(u => u.Id == requesterUserId.Value);
            if (!userExists)
                throw new ApiErrorException(BaseErrorCodes.ValidationFailed, "Requester user does not exist.");

            return (requesterUserId.Value, null);
        }

        if (requesterType == ApprovalRequesterType.Group)
        {
            if (!requesterGroupId.HasValue || requesterGroupId.Value <= 0)
                throw new ApiErrorException(BaseErrorCodes.ValidationFailed, "Requester group is required.");

            var groupExists = await context.UserGroups.AnyAsync(g =>
                g.Id == requesterGroupId.Value && g.CompanyDb == CompanyDb);
            if (!groupExists)
                throw new ApiErrorException(BaseErrorCodes.ValidationFailed, "Requester group does not exist.");

            return (null, requesterGroupId.Value);
        }

        throw new ApiErrorException(BaseErrorCodes.ValidationFailed, "Invalid requester type.");
    }

    /// <summary>
    /// Only one active policy may exist per (requester user|group, document type). Matching prefers
    /// a user-specific policy over a group policy, so two active user policies for the same pair
    /// would make routing non-deterministic.
    /// </summary>
    private async Task ValidateNoActiveDuplicateAsync(
        ApprovalDocumentType docType,
        ApprovalRequesterType requesterType,
        int? requesterUserId,
        int? requesterGroupId,
        int? excludePolicyId)
    {
        var duplicateExists = requesterType == ApprovalRequesterType.User
            ? await context.ApprovalPolicies.AnyAsync(p =>
                p.CompanyDb == CompanyDb &&
                p.DocumentType == docType &&
                p.RequesterType == ApprovalRequesterType.User &&
                p.RequesterUserId == requesterUserId &&
                p.IsActive &&
                p.Id != excludePolicyId)
            : await context.ApprovalPolicies.AnyAsync(p =>
                p.CompanyDb == CompanyDb &&
                p.DocumentType == docType &&
                p.RequesterType == ApprovalRequesterType.Group &&
                p.RequesterGroupId == requesterGroupId &&
                p.IsActive &&
                p.Id != excludePolicyId);

        if (duplicateExists)
            throw new ApiErrorException(
                BaseErrorCodes.Conflict,
                "An active approval policy already exists for this requester and document type.");
    }

    #endregion
}
