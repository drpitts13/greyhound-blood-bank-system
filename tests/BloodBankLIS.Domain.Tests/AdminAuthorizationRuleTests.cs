using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class AdminAuthorizationRuleTests
{
    [Fact]
    public void CreateUser_WithoutPermission_IsHardStop()
    {
        var result = AdminAuthorizationRule.EvaluateCreateUser(hasAdminUsersManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AdminAuthorizationRule.CreateUserCode, result.Code);
    }

    [Fact]
    public void CreateUser_WithPermission_Passes()
    {
        var result = AdminAuthorizationRule.EvaluateCreateUser(hasAdminUsersManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void UpdateUser_WithoutPermission_IsHardStop()
    {
        var result = AdminAuthorizationRule.EvaluateUpdateUser(hasAdminUsersManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AdminAuthorizationRule.UpdateUserCode, result.Code);
    }

    [Fact]
    public void UpdateUser_WithPermission_Passes()
    {
        var result = AdminAuthorizationRule.EvaluateUpdateUser(hasAdminUsersManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void AssignRoles_WithoutPermission_IsHardStop()
    {
        var result = AdminAuthorizationRule.EvaluateAssignRoles(hasAdminUsersManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AdminAuthorizationRule.AssignRolesCode, result.Code);
    }

    [Fact]
    public void AssignRoles_WithPermission_Passes()
    {
        var result = AdminAuthorizationRule.EvaluateAssignRoles(hasAdminUsersManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void CreateRole_WithoutPermission_IsHardStop()
    {
        var result = AdminAuthorizationRule.EvaluateCreateRole(hasAdminRolesManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AdminAuthorizationRule.CreateRoleCode, result.Code);
    }

    [Fact]
    public void CreateRole_WithPermission_Passes()
    {
        var result = AdminAuthorizationRule.EvaluateCreateRole(hasAdminRolesManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void UpdateRole_WithoutPermission_IsHardStop()
    {
        var result = AdminAuthorizationRule.EvaluateUpdateRole(hasAdminRolesManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AdminAuthorizationRule.UpdateRoleCode, result.Code);
    }

    [Fact]
    public void UpdateRole_WithPermission_Passes()
    {
        var result = AdminAuthorizationRule.EvaluateUpdateRole(hasAdminRolesManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void SetActive_WithoutPermission_IsHardStop()
    {
        var result = AdminAuthorizationRule.EvaluateSetActive(hasAdminUsersManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AdminAuthorizationRule.SetActiveCode, result.Code);
    }

    [Fact]
    public void SetActive_WithPermission_Passes()
    {
        var result = AdminAuthorizationRule.EvaluateSetActive(hasAdminUsersManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void SetLocked_WithoutPermission_IsHardStop()
    {
        var result = AdminAuthorizationRule.EvaluateSetLocked(hasAdminUsersManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AdminAuthorizationRule.SetLockedCode, result.Code);
    }

    [Fact]
    public void SetLocked_WithPermission_Passes()
    {
        var result = AdminAuthorizationRule.EvaluateSetLocked(hasAdminUsersManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void PasswordReset_WithoutPermission_IsHardStop()
    {
        var result = AdminAuthorizationRule.EvaluatePasswordReset(hasAdminUsersManage: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AdminAuthorizationRule.PasswordResetCode, result.Code);
    }

    [Fact]
    public void PasswordReset_WithPermission_Passes()
    {
        var result = AdminAuthorizationRule.EvaluatePasswordReset(hasAdminUsersManage: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
