using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class AntibodyPanelLotAdminRuleTests
{
    [Fact]
    public void Create_WithoutPermission_IsHardStop()
    {
        var result = AntibodyPanelLotAdminRule.EvaluateCreate(hasAdminConfigEdit: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyPanelLotAdminRule.CreatePermCode, result.Code);
    }

    [Fact]
    public void Create_WithPermission_Passes()
    {
        var result = AntibodyPanelLotAdminRule.EvaluateCreate(hasAdminConfigEdit: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void CreateDraft_WithoutCells_IsHardStop()
    {
        var stops = AntibodyPanelLotAdminRule.EvaluateCreateDraft(ValidDraft(cells: []));
        Assert.Contains(stops, r => r.Code == AntibodyPanelLotAdminRule.CellsCode);
    }

    [Fact]
    public void CreateDraft_Expired_IsHardStop()
    {
        var draft = ValidDraft() with { ExpiresOn = new DateOnly(2020, 1, 1), Today = new DateOnly(2026, 9, 4) };
        var stops = AntibodyPanelLotAdminRule.EvaluateCreateDraft(draft);
        Assert.Contains(stops, r => r.Code == AntibodyPanelLotAdminRule.ExpiredCode);
    }

    [Fact]
    public void CreateDraft_PanelCellWithoutAntigen_IsHardStop()
    {
        var draft = ValidDraft(cells:
        [
            new AntibodyPanelLotCreateCellDraft("1", PanelCellRole.Panel, [])
        ]);
        var stops = AntibodyPanelLotAdminRule.EvaluateCreateDraft(draft);
        Assert.Contains(stops, r => r.Code == AntibodyPanelLotAdminRule.AntigenCode);
    }

    [Fact]
    public void CreateDraft_ValidPanelCell_Passes()
    {
        var stops = AntibodyPanelLotAdminRule.EvaluateCreateDraft(ValidDraft());
        Assert.Empty(stops);
    }

    // TEST-BB-958: an antigram can be corrected only before any workup uses the lot, with a reason, and the corrected
    // cells pass the same checks as a new lot's.
    [Fact]
    public void CorrectDraft_InUse_NoReason_OrBadCells_IsHardStop()
    {
        IReadOnlyList<AntibodyPanelLotCreateCellDraft> good =
            [new("1", PanelCellRole.Panel, [new AntibodyPanelLotCreateAntigenDraft(10, AntigenExpression.Present)])];
        var active = new HashSet<long> { 10 };

        var inUse = Assert.Single(AntibodyPanelLotAdminRule.EvaluateCorrectDraft(2, "typo", false, good, active));
        Assert.Equal(AntibodyPanelLotAdminRule.CorrectInUseCode, inUse.Code);
        Assert.Contains("2 workups use this lot", inUse.Message);

        Assert.Contains(AntibodyPanelLotAdminRule.EvaluateCorrectDraft(0, " ", false, good, active),
            r => r.Code == AntibodyPanelLotAdminRule.CorrectReasonCode);

        IReadOnlyList<AntibodyPanelLotCreateCellDraft> duplicate =
        [
            new("1", PanelCellRole.Panel, [new AntibodyPanelLotCreateAntigenDraft(10, AntigenExpression.Present)]),
            new("1", PanelCellRole.Selected, [new AntibodyPanelLotCreateAntigenDraft(99, AntigenExpression.Present)])
        ];
        var stops = AntibodyPanelLotAdminRule.EvaluateCorrectDraft(0, "typo", false, duplicate, active);
        Assert.Contains(stops, r => r.Code == AntibodyPanelLotAdminRule.CellsCode && r.Message.Contains("duplicated"));
        Assert.Contains(stops, r => r.Code == AntibodyPanelLotAdminRule.CellsCode && r.Message.Contains("Primary panels"));
        Assert.Contains(stops, r => r.Code == AntibodyPanelLotAdminRule.AntigenCode);
        Assert.Contains(AntibodyPanelLotAdminRule.EvaluateCorrectDraft(0, "typo", false, [], active),
            r => r.Message.StartsWith("A corrected antigram requires at least one cell"));

        Assert.Empty(AntibodyPanelLotAdminRule.EvaluateCorrectDraft(0, "typo", false, good, active));
        Assert.Equal(RuleSeverity.HardStop, AntibodyPanelLotAdminRule.EvaluateCorrect(false).Severity);
    }

    private static AntibodyPanelLotCreateDraft ValidDraft(
        IReadOnlyList<AntibodyPanelLotCreateCellDraft>? cells = null) =>
        new(
            ManufacturerIsActive: true,
            LotNumber: "GHP-NEW-1",
            PanelName: "New panel",
            ExpiresOn: new DateOnly(2027, 12, 31),
            Today: new DateOnly(2026, 9, 4),
            DuplicateLotNumber: false,
            IsSelectedCellLot: false,
            Cells: cells ??
            [
                new AntibodyPanelLotCreateCellDraft(
                    "1",
                    PanelCellRole.Panel,
                    [new AntibodyPanelLotCreateAntigenDraft(10, AntigenExpression.Present)])
            ],
            ActiveAttributeIds: new HashSet<long> { 10 });

    [Fact]
    public void Activate_WithoutPermission_IsHardStop()
    {
        var result = AntibodyPanelLotAdminRule.EvaluateActivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyPanelLotAdminRule.ActivatePermCode, result.Code);
    }

    [Fact]
    public void Activate_WithPermission_Passes()
    {
        var result = AntibodyPanelLotAdminRule.EvaluateActivate(hasAdminConfigActivate: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Deactivate_WithoutPermission_IsHardStop()
    {
        var result = AntibodyPanelLotAdminRule.EvaluateDeactivate(hasAdminConfigActivate: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyPanelLotAdminRule.DeactivatePermCode, result.Code);
    }

    [Fact]
    public void Deactivate_WithoutReason_IsHardStop()
    {
        var result = AntibodyPanelLotAdminRule.EvaluateDeactivateReason(null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(AntibodyPanelLotAdminRule.DeactivateReasonCode, result.Code);
    }

    [Fact]
    public void Deactivate_WithReason_Passes()
    {
        var result = AntibodyPanelLotAdminRule.EvaluateDeactivateReason("QC failure.");
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
