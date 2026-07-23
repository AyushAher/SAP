using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Shared;
using SapApi.Shared.Exceptions;

namespace SapApi.Infrastructure.Services;

public class UserGroupService(AppDbContext context, ICurrentCompanyDbAccessor companyDbAccessor)
{
    private string CompanyDb => companyDbAccessor.GetCompanyDbName();

    public async Task<List<UserGroup>> GetAllAsync()
    {
        return await context.UserGroups
            .Where(g => g.CompanyDb == CompanyDb)
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<UserGroup?> GetByIdAsync(int id)
    {
        return await context.UserGroups
            .Where(g => g.CompanyDb == CompanyDb)
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<int> CreateAsync(string name, string? description, List<int> memberUserIds)
    {
        var trimmedName = ValidateName(name);
        await ValidateUniqueNameAsync(trimmedName, excludeGroupId: null);
        await ValidateMembersExistAsync(memberUserIds);
        await ValidateMembersNotInOtherGroupAsync(memberUserIds, excludeGroupId: null);

        var group = new UserGroup
        {
            CompanyDb = CompanyDb,
            Name = trimmedName,
            Description = NormalizeDescription(description),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Members = DistinctMembers(memberUserIds),
        };

        context.UserGroups.Add(group);
        await context.SaveChangesAsync();
        return group.Id;
    }

    public async Task UpdateAsync(int id, string name, string? description, List<int> memberUserIds)
    {
        var trimmedName = ValidateName(name);
        await ValidateUniqueNameAsync(trimmedName, excludeGroupId: id);
        await ValidateMembersExistAsync(memberUserIds);
        await ValidateMembersNotInOtherGroupAsync(memberUserIds, excludeGroupId: id);

        var group = await context.UserGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == id && g.CompanyDb == CompanyDb)
            ?? throw new ApiErrorException(BaseErrorCodes.NullValue, "User group not found.");

        group.Name = trimmedName;
        group.Description = NormalizeDescription(description);

        context.UserGroupMembers.RemoveRange(group.Members);
        group.Members = DistinctMembers(memberUserIds);

        context.UserGroups.Update(group);
        await context.SaveChangesAsync();
    }

    public async Task SetActiveAsync(int id, bool isActive)
    {
        var group = await context.UserGroups
            .FirstOrDefaultAsync(g => g.Id == id && g.CompanyDb == CompanyDb)
            ?? throw new ApiErrorException(BaseErrorCodes.NullValue, "User group not found.");

        group.IsActive = isActive;
        context.UserGroups.Update(group);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var group = await context.UserGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == id && g.CompanyDb == CompanyDb);

        if (group == null)
            return;

        var usedByPolicy = await context.ApprovalPolicies.AnyAsync(p =>
            p.CompanyDb == CompanyDb && p.RequesterGroupId == id);

        if (usedByPolicy)
            throw new ApiErrorException(
                BaseErrorCodes.Conflict,
                "Cannot delete this group because it is used by one or more approval policies.");

        context.UserGroupMembers.RemoveRange(group.Members);
        context.UserGroups.Remove(group);
        await context.SaveChangesAsync();
    }

    private static string ValidateName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ApiErrorException(BaseErrorCodes.ValidationFailed, "Group name is required.");
        if (trimmed.Length > 100)
            throw new ApiErrorException(BaseErrorCodes.ValidationFailed, "Group name must be 100 characters or fewer.");
        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static List<UserGroupMember> DistinctMembers(List<int> memberUserIds) =>
        (memberUserIds ?? [])
            .Where(id => id > 0)
            .Distinct()
            .Select(id => new UserGroupMember { UserId = id })
            .ToList();

    private async Task ValidateUniqueNameAsync(string name, int? excludeGroupId)
    {
        var exists = await context.UserGroups.AnyAsync(g =>
            g.CompanyDb == CompanyDb &&
            g.Name == name &&
            g.Id != excludeGroupId);

        if (exists)
            throw new ApiErrorException(BaseErrorCodes.Conflict, "A user group with this name already exists.");
    }

    private async Task ValidateMembersExistAsync(List<int> memberUserIds)
    {
        var ids = (memberUserIds ?? []).Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return;

        var existingCount = await context.Users.CountAsync(u => ids.Contains(u.Id));
        if (existingCount != ids.Count)
            throw new ApiErrorException(BaseErrorCodes.ValidationFailed, "One or more selected users do not exist.");
    }

    /// <summary>
    /// Each user may belong to at most one group. When updating a group, existing members of that
    /// group are allowed to remain; membership in any other group is rejected.
    /// </summary>
    private async Task ValidateMembersNotInOtherGroupAsync(List<int> memberUserIds, int? excludeGroupId)
    {
        var ids = (memberUserIds ?? []).Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return;

        var conflicts = await (
            from member in context.UserGroupMembers
            join groupEntity in context.UserGroups on member.UserGroupId equals groupEntity.Id
            where ids.Contains(member.UserId) && member.UserGroupId != excludeGroupId
            select new { member.UserId, GroupName = groupEntity.Name }
        ).ToListAsync();

        if (conflicts.Count == 0)
            return;

        var details = string.Join(", ", conflicts
            .OrderBy(c => c.UserId)
            .Select(c => $"user #{c.UserId} is already in “{c.GroupName}”"));

        throw new ApiErrorException(
            BaseErrorCodes.Conflict,
            $"A user can belong to only one group. {details}.");
    }
}
