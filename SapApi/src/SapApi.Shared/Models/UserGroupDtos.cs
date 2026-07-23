namespace SapApi.Shared.Models;

public class UserGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<UserGroupMemberDto> Members { get; set; } = [];
}

public class UserGroupMemberDto
{
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
}

public class UpsertUserGroupRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<int> MemberUserIds { get; set; } = [];
}

public class SetUserGroupActiveRequest
{
    public bool IsActive { get; set; }
}
