using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Identity;

/// <summary>A role aggregates permissions (table <c>Roles</c>). <see cref="Name"/> is unique.</summary>
public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Numeric privilege rank used by exception override gates. Higher values may
    /// override exceptions whose <c>MinSecurityLevel</c> is at or below this level.
    /// </summary>
    public int SecurityLevel { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
