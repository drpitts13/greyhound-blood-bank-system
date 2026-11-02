using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-353
public class SecondAboSeparateSpecimenTests
{
    private static readonly AboRh OPos = new(AboGroup.O, RhType.Positive);

    [Fact]
    public void SameSpecimen_IsNotASecondDetermination() =>
        Assert.False(SecondAboDeterminationRule.HasSecondConcordant(
        [
            new(OPos, IsCurrent: true, SpecimenId: 7),
            new(OPos, IsCurrent: false, SpecimenId: 7)
        ]));

    [Fact]
    public void DifferentSpecimen_IsASecondDetermination() =>
        Assert.True(SecondAboDeterminationRule.HasSecondConcordant(
        [
            new(OPos, IsCurrent: true, SpecimenId: 7),
            new(OPos, IsCurrent: false, SpecimenId: 8)
        ]));

    [Fact]
    public void HistoricalTypeWithNoSpecimen_StillCounts() =>
        Assert.True(SecondAboDeterminationRule.HasSecondConcordant(
        [
            new(OPos, IsCurrent: true, SpecimenId: 7),
            new(OPos, IsCurrent: false)
        ]));

    [Fact]
    public void IssueMessage_SaysSeparateSpecimens() =>
        Assert.Contains("separate specimens",
            SecondAboDeterminationRule.EvaluateForCellularIssue(true, false, ComponentClass.RedBloodCells, false).Message);
}
