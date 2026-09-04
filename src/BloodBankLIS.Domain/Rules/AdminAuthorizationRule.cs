namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for directory user and role-catalog mutations that grant
/// clinical privileges used at identification and issue.
/// </summary>
public static class AdminAuthorizationRule
{
    public const string CreateUserCode = "USR-CREATE-PERM";
    public const string UpdateUserCode = "USR-UPD-PERM";
    public const string AssignRolesCode = "USR-ASSIGN-PERM";
    public const string CreateRoleCode = "ROLE-CREATE-PERM";
    public const string UpdateRoleCode = "ROLE-UPD-PERM";

    public static RuleResult EvaluateCreateUser(bool hasAdminUsersManage) =>
        hasAdminUsersManage
            ? RuleResult.Pass(CreateUserCode)
            : RuleResult.HardStop(
                CreateUserCode,
                "Creating a directory user requires the admin.users.manage permission.");

    public static RuleResult EvaluateUpdateUser(bool hasAdminUsersManage) =>
        hasAdminUsersManage
            ? RuleResult.Pass(UpdateUserCode)
            : RuleResult.HardStop(
                UpdateUserCode,
                "Updating a directory user requires the admin.users.manage permission.");

    public static RuleResult EvaluateAssignRoles(bool hasAdminUsersManage) =>
        hasAdminUsersManage
            ? RuleResult.Pass(AssignRolesCode)
            : RuleResult.HardStop(
                AssignRolesCode,
                "Assigning roles requires the admin.users.manage permission.");

    public static RuleResult EvaluateCreateRole(bool hasAdminRolesManage) =>
        hasAdminRolesManage
            ? RuleResult.Pass(CreateRoleCode)
            : RuleResult.HardStop(
                CreateRoleCode,
                "Creating a role requires the admin.roles.manage permission.");

    public static RuleResult EvaluateUpdateRole(bool hasAdminRolesManage) =>
        hasAdminRolesManage
            ? RuleResult.Pass(UpdateRoleCode)
            : RuleResult.HardStop(
                UpdateRoleCode,
                "Updating a role's permissions requires the admin.roles.manage permission.");
}
