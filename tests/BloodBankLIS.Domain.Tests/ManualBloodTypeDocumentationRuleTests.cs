using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-355 (OCD-048)
public class ManualBloodTypeDocumentationRuleTests
{
    private static PatientBloodTypeHistory Row(bool current, BloodTypeSource source, ManualTypeDocumentation? doc = null, long? specimen = null) =>
        new() { Abo = AboGroup.A, RhD = RhType.Positive, IsCurrent = current, Source = source, ManualDocumentation = doc, SpecimenId = specimen };

    [Fact]
    public void UndocumentedManualType_IsNeverADetermination()
    {
        // Undocumented manual as the earlier type.
        Assert.False(SecondAboDeterminationRule.HasSecondConcordant(SecondAboDeterminationRule.FromHistory(
            [Row(true, BloodTypeSource.TestResult, specimen: 1), Row(false, BloodTypeSource.ManualEntry, ManualTypeDocumentation.NotDocumented)])));
        // Undocumented manual as the current type, and older rows with no documentation recorded at all.
        Assert.False(SecondAboDeterminationRule.HasSecondConcordant(SecondAboDeterminationRule.FromHistory(
            [Row(true, BloodTypeSource.ManualEntry), Row(false, BloodTypeSource.TestResult, specimen: 1)])));
    }

    [Theory]
    [InlineData(ManualTypeDocumentation.PreviousBloodBankSystem)]
    [InlineData(ManualTypeDocumentation.OutsideSource)]
    public void DocumentedManualType_Counts(ManualTypeDocumentation doc) =>
        Assert.True(SecondAboDeterminationRule.HasSecondConcordant(SecondAboDeterminationRule.FromHistory(
            [Row(true, BloodTypeSource.TestResult, specimen: 1), Row(false, BloodTypeSource.ManualEntry, doc)])));

    [Fact]
    public void ImportedHistoryFromAPreviousSystem_StillCounts() =>
        Assert.True(SecondAboDeterminationRule.HasSecondConcordant(SecondAboDeterminationRule.FromHistory(
            [Row(true, BloodTypeSource.TestResult, specimen: 1), Row(false, BloodTypeSource.HistoricalImport)])));

    [Theory]
    [InlineData(ManualTypeDocumentation.NotDocumented, null, RuleSeverity.Pass)]
    [InlineData(ManualTypeDocumentation.OutsideSource, " ", RuleSeverity.HardStop)]
    [InlineData(ManualTypeDocumentation.PreviousBloodBankSystem, "Legacy LIS record 1029", RuleSeverity.Pass)]
    public void DocumentedSource_NeedsAReference(ManualTypeDocumentation doc, string? reference, RuleSeverity expected) =>
        Assert.Equal(expected, ManualBloodTypeDocumentationRule.Evaluate(doc, reference).Severity);
}
