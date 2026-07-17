using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Identity;

/// <summary>Join of a user to a role (table <c>UserRoles</c>); unique pair.</summary>
public class UserRole : BaseEntity
{
    public long UserId { get; set; }

    public User? User { get; set; }

    public long RoleId { get; set; }

    public Role? Role { get; set; }
}
