using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Domain.Tests;

public class ReactionWorkupRulesTests
{
    private static readonly DateTime Issued = new(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);

    // TEST-BB-445: the pre-transfusion specimen is the patient's, usable, and collected no later than the issue.
    [Fact]
    public void PreSpecimen_MustBeThePatients_Usable_AndCollectedBeforeTheIssue()
    {
        Assert.Equal(RuleSeverity.Pass, ReactionWorkupSpecimenRule.EvaluatePre(1, 1, SpecimenStatus.Accepted, Issued, Issued, "A1").Severity);
        Assert.Equal(RuleSeverity.Pass, ReactionWorkupSpecimenRule.EvaluatePre(1, 1, SpecimenStatus.Expired, Issued.AddDays(-4), Issued, "A1").Severity);

        var after = ReactionWorkupSpecimenRule.EvaluatePre(1, 1, SpecimenStatus.Accepted, Issued.AddMinutes(1), Issued, "A1");
        Assert.Equal(RuleSeverity.HardStop, after.Severity);
        Assert.Contains("after the unit was issued", after.Message);
        Assert.Contains("another patient", ReactionWorkupSpecimenRule.EvaluatePre(1, 2, SpecimenStatus.Accepted, Issued, Issued, "A1").Message);
        Assert.Contains("Rejected", ReactionWorkupSpecimenRule.EvaluatePre(1, 1, SpecimenStatus.Rejected, Issued, Issued, "A1").Message);
    }

    // TEST-BB-445: the post-transfusion specimen is a new, accepted tube collected after the transfusion began.
    [Fact]
    public void PostSpecimen_MustBeANewAcceptedTube_CollectedAfterTheStart()
    {
        var start = Issued.AddMinutes(30);
        Assert.Equal(RuleSeverity.Pass, ReactionWorkupSpecimenRule.EvaluatePost(1, 1, SpecimenStatus.Accepted, start, start, 9, 8, "P1").Severity);
        Assert.Contains("before the transfusion began",
            ReactionWorkupSpecimenRule.EvaluatePost(1, 1, SpecimenStatus.Accepted, start.AddMinutes(-1), start, 9, 8, "P1").Message);
        Assert.Contains("already the pre-transfusion specimen",
            ReactionWorkupSpecimenRule.EvaluatePost(1, 1, SpecimenStatus.Accepted, start.AddHours(1), start, 8, 8, "P1").Message);
        Assert.Contains("Expired",
            ReactionWorkupSpecimenRule.EvaluatePost(1, 1, SpecimenStatus.Expired, start.AddHours(1), start, 9, 8, "P1").Message);
        Assert.Contains("another patient",
            ReactionWorkupSpecimenRule.EvaluatePost(1, 3, SpecimenStatus.Accepted, start.AddHours(1), start, 9, 8, "P1").Message);
    }

    // TEST-BB-445 (OCD-058): only a reaction workup order may use an expired specimen.
    [Fact]
    public void ExpiredSpecimens_AreAllowedOnlyForReactionWorkupOrders()
    {
        Assert.True(ReactionWorkupSpecimenRule.AllowsExpiredSpecimen(OrderType.TransfusionReactionWorkup));
        Assert.All(Enum.GetValues<OrderType>().Where(t => t != OrderType.TransfusionReactionWorkup),
            t => Assert.False(ReactionWorkupSpecimenRule.AllowsExpiredSpecimen(t)));
    }

    // TEST-BB-446: the close gate checks the checklist, both specimens, verified tests, then the elution.
    [Fact]
    public void CloseGate_NamesTheNextMissingStep_InOrder()
    {
        ReactionWorkupCompletenessRule.WorkupState State(bool pre = true, bool post = true, int prePending = 0, int postPending = 0, bool dat = false) =>
            new(pre, post, prePending, postPending, dat);
        string? Message(bool clerical, bool visual, ReactionWorkupCompletenessRule.WorkupState s, string? elution = null)
        {
            var r = ReactionWorkupCompletenessRule.Evaluate(clerical, visual, s, elution);
            return r.Severity == RuleSeverity.HardStop ? r.Message : null;
        }

        Assert.Contains("Clerical check", Message(false, true, State()));
        Assert.Contains("Visual inspection", Message(true, false, State()));
        Assert.Equal("Choose the pre-transfusion specimen to order its workup tests.", Message(true, true, State(pre: false, post: false)));
        Assert.Equal("Accession or choose the post-transfusion specimen to order its workup tests.", Message(true, true, State(post: false)));
        Assert.Equal("Result and verify the workup tests: 3 still to verify.", Message(true, true, State(prePending: 1, postPending: 2)));
        Assert.Contains("post-transfusion DAT is positive", Message(true, true, State(dat: true)));
        Assert.Null(Message(true, true, State(dat: true), "Anti-Jka eluted"));
        Assert.Null(Message(true, true, State()));
    }

    // TEST-BB-442: a chosen group must be active and released; an empty role is allowed (ordering is then refused).
    [Fact]
    public void Settings_AcceptOnlyReleasedGroups_AndNeedAReason()
    {
        var released = new TestGrouper { Code = "TRX-PRE", IsActive = true, IsDraft = false };
        var draft = new TestGrouper { Code = "TRX-POST", IsActive = false, IsDraft = true };
        var settings = new ReactionWorkupSettings { PreTransfusionTestGroupCode = "TRX-PRE", PostTransfusionTestGroupCode = "TRX-POST" };

        var result = ReactionWorkupSettingsValidator.Validate(settings, "Initial setup", released, draft);
        Assert.Equal(new[] { ReactionWorkupSettingsValidator.PostGroupCode }, result.HardStops.Select(r => r.Code).ToArray());
        Assert.Contains(ReactionWorkupSettingsValidator.PreGroupCode,
            ReactionWorkupSettingsValidator.Validate(settings, "x", null, released).HardStops.Select(r => r.Code));
        Assert.Contains(ReactionWorkupSettingsValidator.ReasonRequiredCode,
            ReactionWorkupSettingsValidator.Validate(settings, " ", released, released).HardStops.Select(r => r.Code));
        Assert.False(ReactionWorkupSettingsValidator.Validate(new ReactionWorkupSettings(), "Not yet chosen", null, null).IsHardStopped);
    }
}
