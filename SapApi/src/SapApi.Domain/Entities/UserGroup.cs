namespace SapApi.Domain.Entities;

public class UserGroup
{
    public int Id { get; set; }

    public string CompanyDb { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserGroupMember> Members { get; set; } = [];
}
