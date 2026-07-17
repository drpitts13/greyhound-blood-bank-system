using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities.Identity;

/// <summary>
/// A fine-grained, use-case permission (table <c>Permissions</c>). <see cref="Code"/>
/// is unique (e.g. <c>inventory.issue</c>). Authorization checks evaluate codes, not
/// role strings (see docs/architecture.md 4.2 and <see cref="Rules.PermissionCodes"/>).
/// </summary>
public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
