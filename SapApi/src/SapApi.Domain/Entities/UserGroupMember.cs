namespace SapApi.Domain.Entities;

public class UserGroupMember
{
    public int Id { get; set; }

    public int UserGroupId { get; set; }

    public int UserId { get; set; }

    public UserGroup Group { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
