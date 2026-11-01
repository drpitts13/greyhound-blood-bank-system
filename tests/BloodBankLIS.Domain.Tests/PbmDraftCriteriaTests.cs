using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-319: the unverified PBM draft set loads inactive and cannot be activated until reviewed and renamed (OCD-038).
public class PbmDraftCriteriaTests
{
    private static TransfusionIndicationCriterion From(PbmDraftCriteria.Draft d, bool active) => new()
    {
        Code = d.Code, Name = d.Name, ComponentClass = d.ComponentClass, LabTestCode = d.LabTestCode, Comparison = d.Comparison,
        Threshold = d.Threshold, Unit = d.Unit, LookbackHours = d.LookbackHours, SortOrder = d.SortOrder, IsActive = active
    };

    [Fact]
    public void EveryDraft_IsValidWhileInactive_AndMarkedDraft()
    {
        Assert.NotEmpty(PbmDraftCriteria.All);
        Assert.Equal(PbmDraftCriteria.All.Count, PbmDraftCriteria.All.Select(d => d.Code).Distinct().Count());
        foreach (var d in PbmDraftCriteria.All)
        {
            Assert.True(PbmDraftCriteria.IsDraft(d.Code, d.Name), d.Code);
            Assert.StartsWith("DRAFT (unverified): ", d.Name);
            var validation = TransfusionIndicationRule.Validate(From(d, active: false), duplicateCode: false);
            Assert.False(validation.IsHardStopped, d.Code + ": " + string.Join("; ", validation.Results.Select(r => r.Message)));
        }
    }

    [Fact]
    public void EveryDraft_IsRefusedWhenActive()
    {
        foreach (var d in PbmDraftCriteria.All)
        {
            var validation = TransfusionIndicationRule.Validate(From(d, active: true), duplicateCode: false);
            Assert.Contains(validation.HardStops, r => r.Code == TransfusionIndicationRule.DraftCode);
        }
    }

    [Theory]
    [InlineData("DRAFT-HB7", "Hemoglobin below 7", true)]
    [InlineData("HB7", "DRAFT (unverified): Hemoglobin below 7", true)]
    [InlineData("HB7", "  draft hemoglobin", true)]
    [InlineData("HB7", "Hemoglobin below 7 g/dL", false)]
    public void IsDraft_LooksAtCodeAndName(string code, string name, bool expected) =>
        Assert.Equal(expected, PbmDraftCriteria.IsDraft(code, name));

    [Fact]
    public void RenamedDraft_CanBeActive()
    {
        var reviewed = From(PbmDraftCriteria.All[0], active: true);
        reviewed.Code = "HB7";
        reviewed.Name = "Hemoglobin below 7 g/dL, hospitalized stable adult";

        Assert.False(TransfusionIndicationRule.Validate(reviewed, duplicateCode: false).IsHardStopped);
    }
}
