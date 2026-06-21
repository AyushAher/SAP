using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Shared.Enums;

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

        ApprovalPolicy policy = await context.ApprovalPolicies
            .Include(p => p.Approvers)
            .Include(p => p.Rules)
            .FirstAsync(p => p.Id == policyId && p.CompanyDb == CompanyDb);

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
            throw new Exception("At least one approver required.");

        if (approvers.Any(x => x.Priority < 1))
            throw new Exception("Priority must start from 1.");

        if (!approvers.Any(x => x.Priority == 1))
            throw new Exception("At least one Priority 1 approver required.");

        var duplicateUsers = approvers
            .GroupBy(x => x.ApproverUserId)
            .Any(g => g.Count() > 1);

        if (duplicateUsers)
            throw new Exception("Duplicate approvers not allowed.");
    }

    #endregion
}
