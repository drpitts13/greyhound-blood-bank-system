using BloodBankLIS.Domain.Instruments;

namespace BloodBankLIS.Domain.Tests;

public class InstrumentInterpretationValueTests
{
    // TEST-BB-1170: interpretations already in the LIS's form are recognised; the instrument's own wording is not
    // (end-user testing pass 148).
    [Theory]
    [InlineData("Negative", true)]
    [InlineData(" positive ", true)]
    [InlineData("Compatible", true)]
    [InlineData("Incompatible", true)]
    [InlineData("B|Positive", true)]
    [InlineData("B Pos", false)]
    [InlineData("Positive (Perform DAT Assay)", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsLocal_RecognisesLisInterpretations(string? interpretation, bool expected) =>
        Assert.Equal(expected, InstrumentInterpretationValue.IsLocal(interpretation));
}
