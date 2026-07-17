using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Identity;

/// <summary>Join of a role to a permission (table <c>RolePermissions</c>); unique pair.</summary>
public class RolePermission : BaseEntity
{
    public long RoleId { get; set; }

    public Role? Role { get; set; }

    public long PermissionId { get; set; }

    public Permission? Permission { get; set; }
}
