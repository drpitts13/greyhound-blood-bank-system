using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class PatientUnmergeRuleTests
{
    [Fact]
    public void Permission_RequiresPatientMerge()
    {
        var missing = PatientUnmergeRule.EvaluatePermission(false);
        Assert.Equal(RuleSeverity.HardStop, missing.Severity);
        Assert.Equal(PatientUnmergeRule.PermissionCode, missing.Code);
        Assert.Contains("patient.merge", missing.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(RuleSeverity.Pass, PatientUnmergeRule.EvaluatePermission(true).Severity);
    }

    [Fact]
    public void BlankReason_IsHardStopped()
    {
        var result = FirstStop("  ", PatientStatus.Merged, ledgerFound: true, alreadyUnmerged: false, activity: false, changed: false, drifted: false);
        Assert.Equal(PatientUnmergeRule.ReasonCode, result.Code);
    }

    [Fact]
    public void AlreadyUndone_IsHardStopped()
    {
        var result = FirstStop("Wrong patient.", PatientStatus.Active, ledgerFound: false, alreadyUnmerged: true, activity: false, changed: false, drifted: false);
        Assert.Equal(PatientUnmergeRule.StatusCode, result.Code);
        Assert.Contains("already been undone", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingLedger_IsHardStopped()
    {
        var result = FirstStop("Old merge.", PatientStatus.Merged, ledgerFound: false, alreadyUnmerged: false, activity: false, changed: false, drifted: false);
        Assert.Equal(PatientUnmergeRule.LedgerCode, result.Code);
    }

    [Fact]
    public void ActivitySinceMerge_IsHardStopped()
    {
        var result = FirstStop("Wrong patient.", PatientStatus.Merged, ledgerFound: true, alreadyUnmerged: false, activity: true, changed: false, drifted: false);
        Assert.Equal(PatientUnmergeRule.ActivityCode, result.Code);
    }

    [Fact]
    public void ChangedRowAndDriftedSurvivor_AreHardStopped()
    {
        var changed = FirstStop("Wrong patient.", PatientStatus.Merged, ledgerFound: true, alreadyUnmerged: false, activity: false, changed: true, drifted: false);
        Assert.Equal(PatientUnmergeRule.ChangedCode, changed.Code);

        var drifted = FirstStop("Wrong patient.", PatientStatus.Merged, ledgerFound: true, alreadyUnmerged: false, activity: false, changed: false, drifted: true);
        Assert.Equal(PatientUnmergeRule.DriftCode, drifted.Code);
    }

    [Fact]
    public void CleanUnmerge_Passes()
    {
        var results = PatientUnmergeRule.Evaluate(
            "Wrong patient.",
            PatientStatus.Merged,
            ledgerFound: true,
            ledgerAlreadyUnmerged: false,
            activitySinceMerge: false,
            movedRowChanged: false,
            survivorFieldDrifted: false);
        Assert.All(results, r => Assert.Equal(RuleSeverity.Pass, r.Severity));
    }

    private static RuleResult FirstStop(
        string reason,
        PatientStatus status,
        bool ledgerFound,
        bool alreadyUnmerged,
        bool activity,
        bool changed,
        bool drifted) =>
        PatientUnmergeRule.Evaluate(reason, status, ledgerFound, alreadyUnmerged, activity, changed, drifted)
            .First(r => r.Severity == RuleSeverity.HardStop);
}
