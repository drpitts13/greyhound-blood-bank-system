using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class StandardGradesTests
{
    // TEST-BB-1394 (end-user testing loop 2 tick 12): the standard grades include mixed field (mf), neutral so a reading
    // goes to review, alongside 0, 1+ to 4+, H, w+, +/- and NT.
    [Fact]
    public void StandardGrades_IncludeMixedField()
    {
        var grades = SubtestChoiceDefinitions.DefaultGradedReaction();
        var mf = Assert.Single(grades, g => g.Code == "mf");
        Assert.Equal("Mixed field", mf.Label);
        Assert.Equal(ReactionPolarity.Neutral, mf.Polarity);
        Assert.Equal(["0", "1+", "2+", "3+", "4+", "H", "w+", "+/-", "mf", "NT"], grades.Select(g => g.Code));
    }
}

public class MissingStandardGradesTests
{
    // TEST-BB-1395 (end-user testing loop 2 tick 13): a graded subtest saved before mixed field was added lacks only mf;
    // codes compare ignoring case and surrounding spaces.
    [Fact]
    public void MissingStandardGrades_NamesOnlyWhatIsLacking()
    {
        var saved = new[] { "0", "1+", "2+", "3+", "4+", " h ", "W+", "+/-", "NT" };
        Assert.Equal(["mf"], SubtestChoiceDefinitions.MissingStandardGrades(saved).Select(g => g.Code));
        Assert.Empty(SubtestChoiceDefinitions.MissingStandardGrades(SubtestChoiceDefinitions.DefaultGradedReaction().Select(g => g.Code)));
    }
}
